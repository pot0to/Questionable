﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using LLib;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Functions;
using Questionable.Model.Questing;
using Quest = Questionable.Model.Quest;

namespace Questionable.Controller.Steps.Interactions;

internal static class EquipItem
{
    internal sealed class Factory(IDataManager dataManager, ILoggerFactory loggerFactory) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.EquipItem)
                return null;

            ArgumentNullException.ThrowIfNull(step.ItemId);
            return Equip(step.ItemId.Value);
        }

        private DoEquip Equip(uint itemId)
        {
            var item = dataManager.GetExcelSheet<Item>()!.GetRow(itemId) ??
                       throw new ArgumentOutOfRangeException(nameof(itemId));
            var targetSlots = GetEquipSlot(item) ?? throw new InvalidOperationException("Not a piece of equipment");
            return new DoEquip(itemId, item, targetSlots, dataManager, loggerFactory.CreateLogger<DoEquip>());
        }

        private static List<ushort>? GetEquipSlot(Item item)
        {
            return item.EquipSlotCategory.Row switch
            {
                >= 1 and <= 11 => [(ushort)(item.EquipSlotCategory.Row - 1)],
                12 => [11, 12], // rings
                13 => [0],
                17 => [13], // soul crystal
                _ => null
            };
        }
    }

    private sealed class DoEquip(
        uint itemId,
        Item item,
        List<ushort> targetSlots,
        IDataManager dataManager,
        ILogger<DoEquip> logger) : ITask, IToastAware
    {
        private const int MaxAttempts = 3;

        private static readonly IReadOnlyList<InventoryType> SourceInventoryTypes =
        [
            InventoryType.ArmoryMainHand,
            InventoryType.ArmoryOffHand,
            InventoryType.ArmoryHead,
            InventoryType.ArmoryBody,
            InventoryType.ArmoryHands,
            InventoryType.ArmoryLegs,
            InventoryType.ArmoryFeets,

            InventoryType.ArmoryEar,
            InventoryType.ArmoryNeck,
            InventoryType.ArmoryWrist,
            InventoryType.ArmoryRings,

            InventoryType.ArmorySoulCrystal,

            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4,
        ];

        private int _attempts;
        private DateTime _continueAt = DateTime.MaxValue;

        public bool Start()
        {
            Equip();
            _continueAt = DateTime.Now.AddSeconds(1);
            return true;
        }

        public unsafe ETaskResult Update()
        {
            if (DateTime.Now < _continueAt)
                return ETaskResult.StillRunning;

            InventoryManager* inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
                return ETaskResult.StillRunning;

            foreach (ushort x in targetSlots)
            {
                var itemSlot = inventoryManager->GetInventorySlot(InventoryType.EquippedItems, x);
                if (itemSlot != null && itemSlot->ItemId == itemId)
                    return ETaskResult.TaskComplete;
            }

            Equip();
            _continueAt = DateTime.Now.AddSeconds(1);
            return ETaskResult.StillRunning;
        }

        private unsafe void Equip()
        {
            ++_attempts;
            if (_attempts > MaxAttempts)
                throw new TaskException("Unable to equip gear.");

            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
                return;

            var equippedContainer = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
            if (equippedContainer == null)
                return;

            foreach (ushort slot in targetSlots)
            {
                var itemSlot = equippedContainer->GetInventorySlot(slot);
                if (itemSlot != null && itemSlot->ItemId == itemId)
                {
                    logger.LogInformation("Already equipped {Item}, skipping step", item.Name?.ToString());
                    return;
                }
            }

            foreach (InventoryType sourceInventoryType in SourceInventoryTypes)
            {
                var sourceContainer = inventoryManager->GetInventoryContainer(sourceInventoryType);
                if (sourceContainer == null)
                    continue;

                if (inventoryManager->GetItemCountInContainer(itemId, sourceInventoryType, true) == 0 &&
                    inventoryManager->GetItemCountInContainer(itemId, sourceInventoryType) == 0)
                    continue;

                for (ushort sourceSlot = 0; sourceSlot < sourceContainer->Size; sourceSlot++)
                {
                    var sourceItem = sourceContainer->GetInventorySlot(sourceSlot);
                    if (sourceItem == null || sourceItem->ItemId != itemId)
                        continue;

                    // Move the item to the first available slot
                    ushort targetSlot = targetSlots
                        .Where(x =>
                        {
                            var itemSlot = inventoryManager->GetInventorySlot(InventoryType.EquippedItems, x);
                            return itemSlot == null || itemSlot->ItemId == 0;
                        })
                        .Concat(targetSlots).First();

                    logger.LogInformation(
                        "Equipping item from {SourceInventory}, {SourceSlot} to {TargetInventory}, {TargetSlot}",
                        sourceInventoryType, sourceSlot, InventoryType.EquippedItems, targetSlot);

                    int result = inventoryManager->MoveItemSlot(sourceInventoryType, sourceSlot,
                        InventoryType.EquippedItems, targetSlot, 1);
                    logger.LogInformation("MoveItemSlot result: {Result}", result);
                    return;
                }
            }
        }

        public override string ToString() => $"Equip({item.Name})";

        public bool OnErrorToast(SeString message)
        {
            string? insufficientArmoryChestSpace = dataManager.GetString<LogMessage>(709, x => x.Text);
            if (GameFunctions.GameStringEquals(message.TextValue, insufficientArmoryChestSpace))
                _attempts = MaxAttempts;

            return false;
        }
    }
}
