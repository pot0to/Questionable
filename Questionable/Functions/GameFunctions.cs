﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;
using Action = Lumina.Excel.GeneratedSheets2.Action;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using ContentFinderCondition = Lumina.Excel.GeneratedSheets.ContentFinderCondition;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using Quest = Questionable.Model.Quest;
using TerritoryType = Lumina.Excel.GeneratedSheets.TerritoryType;

namespace Questionable.Functions;

internal sealed unsafe class GameFunctions
{
    private readonly ReadOnlyDictionary<ushort, byte> _territoryToAetherCurrentCompFlgSet;
    private readonly ReadOnlyDictionary<uint, ushort> _contentFinderConditionToContentId;

    private readonly QuestFunctions _questFunctions;
    private readonly IDataManager _dataManager;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly IGameGui _gameGui;
    private readonly Configuration _configuration;
    private readonly ILogger<GameFunctions> _logger;

    public GameFunctions(
        QuestFunctions questFunctions,
        IDataManager dataManager,
        IObjectTable objectTable,
        ITargetManager targetManager,
        ICondition condition,
        IClientState clientState,
        IGameGui gameGui,
        Configuration configuration,
        ILogger<GameFunctions> logger)
    {
        _questFunctions = questFunctions;
        _dataManager = dataManager;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _condition = condition;
        _clientState = clientState;
        _gameGui = gameGui;
        _configuration = configuration;
        _logger = logger;

        _territoryToAetherCurrentCompFlgSet = dataManager.GetExcelSheet<TerritoryType>()!
            .Where(x => x.RowId > 0)
            .Where(x => x.Unknown32 > 0)
            .ToDictionary(x => (ushort)x.RowId, x => x.Unknown32)
            .AsReadOnly();
        _contentFinderConditionToContentId = dataManager.GetExcelSheet<ContentFinderCondition>()!
            .Where(x => x.RowId > 0 && x.Content > 0)
            .ToDictionary(x => x.RowId, x => x.Content)
            .AsReadOnly();
    }

    public DateTime ReturnRequestedAt { get; set; } = DateTime.MinValue;

    public bool IsAetheryteUnlocked(uint aetheryteId, out byte subIndex)
    {
        subIndex = 0;

        var uiState = UIState.Instance();
        return uiState != null && uiState->IsAetheryteUnlocked(aetheryteId);
    }

    public bool IsAetheryteUnlocked(EAetheryteLocation aetheryteLocation)
    {
        if (aetheryteLocation == EAetheryteLocation.IshgardFirmament)
            return _questFunctions.IsQuestComplete(new QuestId(3672));
        return IsAetheryteUnlocked((uint)aetheryteLocation, out _);
    }

    public bool CanTeleport(EAetheryteLocation aetheryteLocation)
    {
        if ((ushort)aetheryteLocation == PlayerState.Instance()->HomeAetheryteId &&
            ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 8) == 0)
            return true;

        return ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) == 0;
    }

    public bool TeleportAetheryte(uint aetheryteId)
    {
        _logger.LogDebug("Attempting to teleport to aetheryte {AetheryteId}", aetheryteId);
        if (IsAetheryteUnlocked(aetheryteId, out var subIndex))
        {
            if (aetheryteId == PlayerState.Instance()->HomeAetheryteId &&
                ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 8) == 0)
            {
                ReturnRequestedAt = DateTime.Now;
                if (ActionManager.Instance()->UseAction(ActionType.GeneralAction, 8))
                {
                    _logger.LogInformation("Using 'return' for home aetheryte");
                    return true;
                }
            }

            if (ActionManager.Instance()->GetActionStatus(ActionType.Action, 5) == 0)
            {
                // fallback if return isn't available or (more likely) on a different aetheryte
                _logger.LogInformation("Teleporting to aetheryte {AetheryteId}", aetheryteId);
                return Telepo.Instance()->Teleport(aetheryteId, subIndex);
            }
        }

        return false;
    }

    public bool TeleportAetheryte(EAetheryteLocation aetheryteLocation)
        => TeleportAetheryte((uint)aetheryteLocation);

    public bool IsFlyingUnlocked(ushort territoryId)
    {
        if (_configuration.Advanced.NeverFly)
            return false;

        if (_questFunctions.IsQuestAccepted(new QuestId(3304)) && _condition[ConditionFlag.Mounted])
        {
            BattleChara* battleChara = (BattleChara*)(_clientState.LocalPlayer?.Address ?? 0);
            if (battleChara != null && battleChara->Mount.MountId == 198) // special quest amaro, not the normal one
                return true;
        }

        var playerState = PlayerState.Instance();
        return playerState != null &&
               _territoryToAetherCurrentCompFlgSet.TryGetValue(territoryId, out byte aetherCurrentCompFlgSet) &&
               playerState->IsAetherCurrentZoneComplete(aetherCurrentCompFlgSet);
    }

    public bool IsFlyingUnlockedInCurrentZone() => IsFlyingUnlocked(_clientState.TerritoryType);

    public bool IsAetherCurrentUnlocked(uint aetherCurrentId)
    {
        var playerState = PlayerState.Instance();
        return playerState != null &&
               playerState->IsAetherCurrentUnlocked(aetherCurrentId);
    }

    public IGameObject? FindObjectByDataId(uint dataId, ObjectKind? kind = null, bool targetable = false)
    {
        foreach (var gameObject in _objectTable)
        {
            if (targetable && !gameObject.IsTargetable)
                continue;

            if (gameObject.ObjectKind is ObjectKind.Player or ObjectKind.Companion or ObjectKind.MountType
                or ObjectKind.Retainer or ObjectKind.Housing)
                continue;

            if (gameObject.DataId == dataId && (kind == null || kind.Value == gameObject.ObjectKind))
            {
                return gameObject;
            }
        }

        _logger.LogWarning("Could not find GameObject with dataId {DataId}", dataId);
        return null;
    }

    public bool InteractWith(uint dataId, ObjectKind? kind = null)
    {
        IGameObject? gameObject = FindObjectByDataId(dataId, kind);
        if (gameObject != null)
            return InteractWith(gameObject);

        _logger.LogDebug("Game object is null");
        return false;
    }

    public bool InteractWith(IGameObject gameObject)
    {
        _logger.LogInformation("Setting target with {DataId} to {ObjectId}", gameObject.DataId, gameObject.EntityId);
        _targetManager.Target = null;
        _targetManager.Target = gameObject;

        if (gameObject.ObjectKind == ObjectKind.GatheringPoint)
        {
            TargetSystem.Instance()->OpenObjectInteraction((GameObject*)gameObject.Address);
            _logger.LogInformation("Interact result: (none) for GatheringPoint");
            return true;
        }
        else
        {
            long result = (long)TargetSystem.Instance()->InteractWithObject((GameObject*)gameObject.Address, false);

            _logger.LogInformation("Interact result: {Result}", result);
            return result != 7 && result > 0;
        }
    }

    public bool UseItem(uint itemId)
    {
        long result = AgentInventoryContext.Instance()->UseItem(itemId);
        _logger.LogInformation("UseItem result: {Result}", result);

        return result == 0;
    }

    public bool UseItem(uint dataId, uint itemId)
    {
        IGameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            _targetManager.Target = gameObject;
            long result = AgentInventoryContext.Instance()->UseItem(itemId);

            _logger.LogInformation("UseItem result on {DataId}: {Result}", dataId, result);

            // TODO is 1 a generally accepted result?
            return result == 0 || (itemId == 2002450 && result == 1);
        }

        return false;
    }

    public bool UseItemOnGround(uint dataId, uint itemId)
    {
        IGameObject? gameObject = FindObjectByDataId(dataId);
        if (gameObject != null)
        {
            Vector3 position = gameObject.Position;
            return ActionManager.Instance()->UseActionLocation(ActionType.KeyItem, itemId, location: &position);
        }

        return false;
    }

    public bool UseItemOnPosition(Vector3 position, uint itemId)
    {
        return ActionManager.Instance()->UseActionLocation(ActionType.KeyItem, itemId, location: &position);
    }

    public bool UseAction(EAction action)
    {
        if (ActionManager.Instance()->GetActionStatus(ActionType.Action, (uint)action) == 0)
        {
            bool result = ActionManager.Instance()->UseAction(ActionType.Action, (uint)action);
            _logger.LogInformation("UseAction {Action} result: {Result}", action, result);

            return result;
        }

        return false;
    }

    public bool UseAction(IGameObject gameObject, EAction action)
    {
        var actionRow = _dataManager.GetExcelSheet<Action>()!.GetRow((uint)action)!;
        if (!ActionManager.CanUseActionOnTarget((uint)action, (GameObject*)gameObject.Address))
        {
            _logger.LogWarning("Can not use action {Action} on target {Target}", action, gameObject);
            return false;
        }

        _targetManager.Target = gameObject;
        if (ActionManager.Instance()->GetActionStatus(ActionType.Action, (uint)action, gameObject.GameObjectId) == 0)
        {
            bool result;
            if (actionRow.TargetArea)
            {
                Vector3 position = gameObject.Position;
                result = ActionManager.Instance()->UseActionLocation(ActionType.Action, (uint)action,
                    location: &position);
                _logger.LogInformation("UseAction {Action} on target area {Target} result: {Result}", action,
                    gameObject,
                    result);
            }
            else
            {
                result = ActionManager.Instance()->UseAction(ActionType.Action, (uint)action, gameObject.GameObjectId);
                _logger.LogInformation("UseAction {Action} on target {Target} result: {Result}", action, gameObject,
                    result);
            }

            return result;
        }

        return false;
    }

    public bool IsObjectAtPosition(uint dataId, Vector3 position, float distance)
    {
        IGameObject? gameObject = FindObjectByDataId(dataId);
        return gameObject != null && (gameObject.Position - position).Length() < distance;
    }

    public bool HasStatusPreventingMount()
    {
        if (_condition[ConditionFlag.Swimming] && !IsFlyingUnlockedInCurrentZone())
            return true;

        // company chocobo is locked
        var playerState = PlayerState.Instance();
        if (playerState != null && !playerState->IsMountUnlocked(1))
            return true;

        var localPlayer = _clientState.LocalPlayer;
        if (localPlayer == null)
            return false;

        var battleChara = (BattleChara*)localPlayer.Address;
        StatusManager* statusManager = battleChara->GetStatusManager();
        if (statusManager->HasStatus(1151))
            return true;

        return HasCharacterStatusPreventingMountOrSprint();
    }

    public bool HasStatusPreventingSprint() => HasCharacterStatusPreventingMountOrSprint();

    private bool HasCharacterStatusPreventingMountOrSprint()
    {
        var localPlayer = _clientState.LocalPlayer;
        if (localPlayer == null)
            return false;

        var battleChara = (BattleChara*)localPlayer.Address;
        StatusManager* statusManager = battleChara->GetStatusManager();
        return statusManager->HasStatus(565) ||
               statusManager->HasStatus(404) ||
               statusManager->HasStatus(416) ||
               statusManager->HasStatus(2729) ||
               statusManager->HasStatus(2730);
    }

    public bool Mount()
    {
        if (_condition[ConditionFlag.Mounted])
            return true;

        var playerState = PlayerState.Instance();
        if (playerState != null && _configuration.General.MountId != 0 &&
            playerState->IsMountUnlocked(_configuration.General.MountId))
        {
            if (ActionManager.Instance()->GetActionStatus(ActionType.Mount, _configuration.General.MountId) == 0)
            {
                _logger.LogDebug("Attempting to use preferred mount...");
                if (ActionManager.Instance()->UseAction(ActionType.Mount, _configuration.General.MountId))
                {
                    _logger.LogInformation("Using preferred mount");
                    return true;
                }

                return false;
            }
        }
        else
        {
            if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 9) == 0)
            {
                _logger.LogDebug("Attempting to use mount roulette...");
                if (ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9))
                {
                    _logger.LogInformation("Using mount roulette");
                    return true;
                }

                return false;
            }
        }

        return false;
    }

    public bool Unmount()
    {
        if (!_condition[ConditionFlag.Mounted])
            return true;

        if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 23) == 0)
        {
            _logger.LogDebug("Attempting to unmount...");
            if (ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23))
            {
                _logger.LogInformation("Unmounted");
                return true;
            }

            return false;
        }
        else
        {
            _logger.LogWarning("Can't unmount right now?");
            return false;
        }
    }

    public void OpenDutyFinder(uint contentFinderConditionId)
    {
        if (_contentFinderConditionToContentId.TryGetValue(contentFinderConditionId, out ushort contentId))
        {
            if (UIState.IsInstanceContentUnlocked(contentId))
                AgentContentsFinder.Instance()->OpenRegularDuty(contentFinderConditionId);
            else
                _logger.LogError(
                    "Trying to access a locked duty (cf: {ContentFinderId}, content: {ContentId})",
                    contentFinderConditionId, contentId);
        }
        else
            _logger.LogError("Could not find content for content finder condition (cf: {ContentFinderId})",
                contentFinderConditionId);
    }

    /// <summary>
    /// Ensures characters like '-' are handled equally in both strings.
    /// </summary>
    public static bool GameStringEquals(string? a, string? b)
    {
        if (a == null)
            return b == null;

        if (b == null)
            return false;

        return a.ReplaceLineEndings().Replace('\u2013', '-') == b.ReplaceLineEndings().Replace('\u2013', '-');
    }

    public bool IsOccupied()
    {
        if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null)
            return true;

        if (IsLoadingScreenVisible())
            return true;

        return _condition[ConditionFlag.Occupied] || _condition[ConditionFlag.Occupied30] ||
               _condition[ConditionFlag.Occupied33] || _condition[ConditionFlag.Occupied38] ||
               _condition[ConditionFlag.Occupied39] || _condition[ConditionFlag.OccupiedInEvent] ||
               _condition[ConditionFlag.OccupiedInQuestEvent] || _condition[ConditionFlag.OccupiedInCutSceneEvent] ||
               _condition[ConditionFlag.Casting] || _condition[ConditionFlag.Unknown57] ||
               _condition[ConditionFlag.BetweenAreas] || _condition[ConditionFlag.BetweenAreas51] ||
               _condition[ConditionFlag.Jumping61] || _condition[ConditionFlag.Gathering42];
    }

    public bool IsOccupiedWithCustomDeliveryNpc(Quest? currentQuest)
    {
        // not a supply quest?
        if (currentQuest is not { Info: SatisfactionSupplyInfo })
            return false;

        if (_targetManager.Target == null || _targetManager.Target.DataId != currentQuest.Info.IssuerDataId)
            return false;

        if (!AgentSatisfactionSupply.Instance()->IsAgentActive())
            return false;

        var flags = _condition.AsReadOnlySet();
        return flags.Count == 2 &&
               flags.Contains(ConditionFlag.NormalConditions) &&
               flags.Contains(ConditionFlag.OccupiedInQuestEvent);
    }

    public bool IsLoadingScreenVisible()
    {
        return _gameGui.TryGetAddonByName("FadeMiddle", out AtkUnitBase* fade) &&
               LAddon.IsAddonReady(fade) &&
               fade->IsVisible;
    }

    public int GetFreeInventorySlots()
    {
        InventoryManager* inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return 0;

        int slots = 0;
        for (InventoryType inventoryType = InventoryType.Inventory1;
             inventoryType <= InventoryType.Inventory4;
             ++inventoryType)
        {
            InventoryContainer* inventoryContainer = inventoryManager->GetInventoryContainer(inventoryType);
            if (inventoryContainer == null)
                continue;

            for (int i = 0; i < inventoryContainer->Size; ++i)
            {
                InventoryItem* item = inventoryContainer->GetInventorySlot(i);
                if (item == null || item->ItemId == 0)
                    ++slots;
            }
        }

        return slots;
    }
}