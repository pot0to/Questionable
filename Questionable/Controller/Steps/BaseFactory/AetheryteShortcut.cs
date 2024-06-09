﻿using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.BaseTasks;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.BaseFactory;

internal static class AetheryteShortcut
{
    internal sealed class Factory(IServiceProvider serviceProvider, GameFunctions gameFunctions) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.AetheryteShortcut == null)
                return [];

            var task = serviceProvider.GetRequiredService<UseAetheryteShortcut>()
                .With(step, step.AetheryteShortcut.Value);
            return [new WaitConditionTask(gameFunctions.CanTeleport, "CanTeleport"), task];
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class UseAetheryteShortcut(
        ILogger<UseAetheryteShortcut> logger,
        GameFunctions gameFunctions,
        IClientState clientState,
        IChatGui chatGui,
        AetheryteData aetheryteData) : ITask
    {
        private DateTime _continueAt;

        public QuestStep Step { get; set; } = null!;
        public EAetheryteLocation TargetAetheryte { get; set; }

        public ITask With(QuestStep step, EAetheryteLocation targetAetheryte)
        {
            Step = step;
            TargetAetheryte = targetAetheryte;
            return this;
        }

        public bool Start()
        {
            _continueAt = DateTime.Now.AddSeconds(8);
            ushort territoryType = clientState.TerritoryType;
            if (Step.TerritoryId == territoryType)
            {
                Vector3 pos = clientState.LocalPlayer!.Position;
                if (aetheryteData.CalculateDistance(pos, territoryType, TargetAetheryte) < 11 ||
                    (Step.AethernetShortcut != null &&
                     (aetheryteData.CalculateDistance(pos, territoryType, Step.AethernetShortcut.From) < 20 ||
                      aetheryteData.CalculateDistance(pos, territoryType, Step.AethernetShortcut.To) < 20)))
                {
                    logger.LogInformation("Skipping aetheryte teleport");
                    return false;
                }
            }

            if (!gameFunctions.IsAetheryteUnlocked(TargetAetheryte))
            {
                chatGui.Print($"[Questionable] Aetheryte {TargetAetheryte} is not unlocked.");
                throw new TaskException("Aetheryte is not unlocked");
            }
            else if (gameFunctions.TeleportAetheryte(TargetAetheryte))
            {
                logger.LogInformation("Travelling via aetheryte...");
                return true;
            }
            else
            {
                chatGui.Print("[Questionable] Unable to teleport to aetheryte.");
                throw new TaskException("Unable to teleport to aetheryte");
            }
        }

        public ETaskResult Update()
        {

            if (DateTime.Now >= _continueAt && clientState.TerritoryType == Step.TerritoryId)
                return ETaskResult.TaskComplete;

            return ETaskResult.StillRunning;
        }

        public override string ToString() => $"UseAetheryte({TargetAetheryte})";
    }
}
