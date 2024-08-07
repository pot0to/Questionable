﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameData;
using LLib.GameUI;
using Microsoft.Extensions.Logging;
using Questionable.Functions;
using Questionable.Model.Gathering;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Gathering;

internal sealed class DoGather(
    GatheringController gatheringController,
    GameFunctions gameFunctions,
    IGameGui gameGui,
    IClientState clientState,
    ICondition condition,
    ILogger<DoGather> logger) : ITask
{
    private const uint StatusGatheringRateUp = 218;

    private GatheringController.GatheringRequest _currentRequest = null!;
    private GatheringNode _currentNode = null!;
    private bool _wasGathering;
    private SlotInfo? _slotToGather;
    private Queue<EAction>? _actionQueue;

    public ITask With(GatheringController.GatheringRequest currentRequest, GatheringNode currentNode)
    {
        _currentRequest = currentRequest;
        _currentNode = currentNode;
        return this;
    }

    public bool Start() => true;

    public unsafe ETaskResult Update()
    {
        if (gatheringController.HasNodeDisappeared(_currentNode))
            return ETaskResult.TaskComplete;

        if (gameFunctions.GetFreeInventorySlots() == 0)
            throw new TaskException("Inventory full");

        if (condition[ConditionFlag.Gathering])
        {
            if (gameGui.TryGetAddonByName("GatheringMasterpiece", out AtkUnitBase* _))
                return ETaskResult.TaskComplete;

            _wasGathering = true;

            if (gameGui.TryGetAddonByName("Gathering", out AddonGathering* addonGathering))
            {
                if (gatheringController.HasRequestedItems())
                {
                    addonGathering->FireCallbackInt(-1);
                }
                else
                {
                    var slots = ReadSlots(addonGathering);
                    if (_currentRequest.Collectability > 0)
                    {
                        var slot = slots.Single(x => x.ItemId == _currentRequest.ItemId);
                        addonGathering->FireCallbackInt(slot.Index);
                    }
                    else
                    {
                        NodeCondition nodeCondition = new NodeCondition(
                            addonGathering->AtkValues[110].UInt,
                            addonGathering->AtkValues[111].UInt);

                        if (_actionQueue != null && _actionQueue.TryPeek(out EAction nextAction))
                        {
                            if (gameFunctions.UseAction(nextAction))
                            {
                                logger.LogInformation("Used action {Action} on node", nextAction);
                                _actionQueue.Dequeue();
                            }

                            return ETaskResult.StillRunning;
                        }

                        _actionQueue = GetNextActions(nodeCondition, slots);
                        if (_actionQueue.Count == 0)
                        {
                            var slot = _slotToGather ?? slots.Single(x => x.ItemId == _currentRequest.ItemId);
                            addonGathering->FireCallbackInt(slot.Index);
                        }
                    }
                }
            }
        }

        return _wasGathering && !condition[ConditionFlag.Gathering]
            ? ETaskResult.TaskComplete
            : ETaskResult.StillRunning;
    }

    private unsafe List<SlotInfo> ReadSlots(AddonGathering* addonGathering)
    {
        var atkValues = addonGathering->AtkValues;
        List<SlotInfo> slots = new List<SlotInfo>();
        for (int i = 0; i < 8; ++i)
        {
            // +8 = new item?
            uint itemId = atkValues[i * 11 + 7].UInt;
            if (itemId == 0)
                continue;

            AtkComponentCheckBox* atkCheckbox = addonGathering->GatheredItemComponentCheckbox[i].Value;

            AtkTextNode* atkGatheringChance = atkCheckbox->UldManager.SearchNodeById(10)->GetAsAtkTextNode();
            if (!int.TryParse(atkGatheringChance->NodeText.ToString(), out int gatheringChance))
                gatheringChance = 0;

            AtkTextNode* atkBoonChance = atkCheckbox->UldManager.SearchNodeById(16)->GetAsAtkTextNode();
            if (!int.TryParse(atkBoonChance->NodeText.ToString(), out int boonChance))
                boonChance = 0;

            AtkComponentNode* atkImage = atkCheckbox->UldManager.SearchNodeById(31)->GetAsAtkComponentNode();
            AtkTextNode* atkQuantity = atkImage->Component->UldManager.SearchNodeById(7)->GetAsAtkTextNode();
            if (!atkQuantity->IsVisible() || !int.TryParse(atkQuantity->NodeText.ToString(), out int quantity))
                quantity = 1;

            var slot = new SlotInfo(i, itemId, gatheringChance, boonChance, quantity);
            slots.Add(slot);
        }

        return slots;
    }

    private Queue<EAction> GetNextActions(NodeCondition nodeCondition, List<SlotInfo> slots)
    {
        uint gp = clientState.LocalPlayer!.CurrentGp;
        Queue<EAction> actions = new();

        if (!gameFunctions.HasStatus(StatusGatheringRateUp))
        {
            // do we have an alternative item? only happens for 'evaluation' leve quests
            if (_currentRequest.AlternativeItemId != 0)
            {
                var alternativeSlot = slots.Single(x => x.ItemId == _currentRequest.AlternativeItemId);

                if (alternativeSlot.GatheringChance == 100)
                {
                    _slotToGather = alternativeSlot;
                    return actions;
                }

                if (alternativeSlot.GatheringChance > 0)
                {
                    if (alternativeSlot.GatheringChance >= 95 &&
                        CanUseAction(EAction.SharpVision1, EAction.FieldMastery1))
                    {
                        _slotToGather = alternativeSlot;
                        actions.Enqueue(PickAction(EAction.SharpVision1, EAction.FieldMastery1));
                        return actions;
                    }

                    if (alternativeSlot.GatheringChance >= 85 &&
                        CanUseAction(EAction.SharpVision2, EAction.FieldMastery2))
                    {
                        _slotToGather = alternativeSlot;
                        actions.Enqueue(PickAction(EAction.SharpVision2, EAction.FieldMastery2));
                        return actions;
                    }

                    if (alternativeSlot.GatheringChance >= 50 &&
                        CanUseAction(EAction.SharpVision3, EAction.FieldMastery3))
                    {
                        _slotToGather = alternativeSlot;
                        actions.Enqueue(PickAction(EAction.SharpVision3, EAction.FieldMastery3));
                        return actions;
                    }
                }
            }

            var slot = slots.Single(x => x.ItemId == _currentRequest.ItemId);
            if (slot.GatheringChance > 0 && slot.GatheringChance < 100)
            {
                if (slot.GatheringChance >= 95 &&
                    CanUseAction(EAction.SharpVision1, EAction.FieldMastery1))
                {
                    actions.Enqueue(PickAction(EAction.SharpVision1, EAction.FieldMastery1));
                    return actions;
                }

                if (slot.GatheringChance >= 85 &&
                    CanUseAction(EAction.SharpVision2, EAction.FieldMastery2))
                {
                    actions.Enqueue(PickAction(EAction.SharpVision2, EAction.FieldMastery2));
                    return actions;
                }

                if (slot.GatheringChance >= 50 &&
                    CanUseAction(EAction.SharpVision3, EAction.FieldMastery3))
                {
                    actions.Enqueue(PickAction(EAction.SharpVision3, EAction.FieldMastery3));
                    return actions;
                }
            }
        }

        return actions;
    }

    private EAction PickAction(EAction minerAction, EAction botanistAction)
    {
        if ((EClassJob?)clientState.LocalPlayer?.ClassJob.Id == EClassJob.Miner)
            return minerAction;
        else
            return botanistAction;
    }

    private unsafe bool CanUseAction(EAction minerAction, EAction botanistAction)
    {
        EAction action = PickAction(minerAction, botanistAction);
        return ActionManager.Instance()->GetActionStatus(ActionType.Action, (uint)action) == 0;
    }

    public override string ToString() => "DoGather";

    private sealed record SlotInfo(int Index, uint ItemId, int GatheringChance, int BoonChance, int Quantity);

    private sealed record NodeCondition(
        uint CurrentIntegrity,
        uint MaxIntegrity);
}
