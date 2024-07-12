﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.Shared;

internal static class WaitAtEnd
{
    internal sealed class Factory(IServiceProvider serviceProvider, IClientState clientState, ICondition condition,
        TerritoryData territoryData)
        : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.CompletionQuestVariablesFlags.Count == 6 && step.CompletionQuestVariablesFlags.Any(x => x is > 0))
            {
                var task = serviceProvider.GetRequiredService<WaitForCompletionFlags>()
                    .With(quest, step);
                var delay = serviceProvider.GetRequiredService<WaitDelay>();
                return [task, delay, Next(quest, sequence)];
            }

            switch (step.InteractionType)
            {
                case EInteractionType.Combat:
                    var notInCombat =
                        new WaitConditionTask(() => !condition[ConditionFlag.InCombat], "Wait(not in combat)");
                    return
                    [
                        serviceProvider.GetRequiredService<WaitDelay>(),
                        notInCombat,
                        serviceProvider.GetRequiredService<WaitDelay>(),
                        Next(quest, sequence)
                    ];

                case EInteractionType.WaitForManualProgress:
                case EInteractionType.ShouldBeAJump:
                case EInteractionType.Instruction:
                    return [serviceProvider.GetRequiredService<WaitNextStepOrSequence>()];

                case EInteractionType.Duty:
                case EInteractionType.SinglePlayerDuty:
                    return [new EndAutomation()];

                case EInteractionType.WalkTo:
                case EInteractionType.Jump:
                    // no need to wait if we're just moving around
                    return [Next(quest, sequence)];

                case EInteractionType.WaitForObjectAtPosition:
                    ArgumentNullException.ThrowIfNull(step.DataId);
                    ArgumentNullException.ThrowIfNull(step.Position);

                    return
                    [
                        serviceProvider.GetRequiredService<WaitObjectAtPosition>()
                            .With(step.DataId.Value, step.Position.Value, step.NpcWaitDistance ?? 0.05f),
                        serviceProvider.GetRequiredService<WaitDelay>(),
                        Next(quest, sequence)
                    ];

                case EInteractionType.Interact when step.TargetTerritoryId != null:
                case EInteractionType.UseItem when step.TargetTerritoryId != null:
                    ITask waitInteraction;
                    if (step.TerritoryId != step.TargetTerritoryId)
                    {
                        // interaction moves to a different territory
                        waitInteraction = new WaitConditionTask(
                            () => clientState.TerritoryType == step.TargetTerritoryId,
                            $"Wait(tp to territory: {territoryData.GetNameAndId(step.TargetTerritoryId.Value)})");
                    }
                    else
                    {
                        Vector3 lastPosition = step.Position ?? clientState.LocalPlayer?.Position ?? Vector3.Zero;
                        waitInteraction = new WaitConditionTask(() =>
                            {
                                Vector3? currentPosition = clientState.LocalPlayer?.Position;
                                if (currentPosition == null)
                                    return false;

                                // interaction moved to elsewhere in the zone
                                // the 'closest' locations are probably
                                //   - waking sands' solar
                                //   - rising stones' solar + dawn's respite
                                return (lastPosition - currentPosition.Value).Length() > 2;
                            }, $"Wait(tp away from {lastPosition.ToString("G", CultureInfo.InvariantCulture)})");
                    }

                    return
                    [
                        waitInteraction,
                        serviceProvider.GetRequiredService<WaitDelay>(),
                        Next(quest, sequence)
                    ];

                case EInteractionType.AcceptQuest:
                    return
                    [
                        serviceProvider.GetRequiredService<WaitQuestAccepted>().With(step.PickupQuestId ?? quest.QuestId),
                        serviceProvider.GetRequiredService<WaitDelay>()
                    ];

                case EInteractionType.CompleteQuest:
                    return
                    [
                        serviceProvider.GetRequiredService<WaitQuestCompleted>().With(step.TurnInQuestId ?? quest.QuestId),
                        serviceProvider.GetRequiredService<WaitDelay>()
                    ];

                case EInteractionType.Interact:
                default:
                    return [serviceProvider.GetRequiredService<WaitDelay>(), Next(quest, sequence)];
            }
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();

        private static NextStep Next(Quest quest, QuestSequence sequence)
        {
            return new NextStep(quest.QuestId, sequence.Sequence);
        }
    }

    internal sealed class WaitDelay() : AbstractDelayedTask(TimeSpan.FromSeconds(1))
    {
        protected override bool StartInternal() => true;

        public override string ToString() => $"Wait(seconds: {Delay.TotalSeconds})";
    }

    internal sealed class WaitNextStepOrSequence : ITask
    {
        public bool Start() => true;

        public ETaskResult Update() => ETaskResult.StillRunning;

        public override string ToString() => "Wait(next step or sequence)";
    }

    internal sealed class WaitForCompletionFlags(GameFunctions gameFunctions) : ITask
    {
        public Quest Quest { get; set; } = null!;
        public QuestStep Step { get; set; } = null!;
        public IList<short?> Flags { get; set; } = null!;

        public ITask With(Quest quest, QuestStep step)
        {
            Quest = quest;
            Step = step;
            Flags = step.CompletionQuestVariablesFlags;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            QuestWork? questWork = gameFunctions.GetQuestEx(Quest.QuestId);
            return questWork != null && Step.MatchesQuestVariables(questWork.Value, false)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;
        }

        public override string ToString() =>
            $"Wait(QW: {string.Join(", ", Flags.Select(x => x?.ToString(CultureInfo.InvariantCulture) ?? "-"))})";
    }

    internal sealed class WaitObjectAtPosition(GameFunctions gameFunctions) : ITask
    {
        public uint DataId { get; set; }
        public Vector3 Destination { get; set; }
        public float Distance { get; set; }

        public ITask With(uint dataId, Vector3 destination, float distance)
        {
            DataId = dataId;
            Destination = destination;
            Distance = distance;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update() =>
            gameFunctions.IsObjectAtPosition(DataId, Destination, Distance)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() =>
            $"WaitObj({DataId} at {Destination.ToString("G", CultureInfo.InvariantCulture)})";
    }

    internal sealed class WaitQuestAccepted : ITask
    {
        public ushort QuestId { get; set; }

        public ITask With(ushort questId)
        {
            QuestId = questId;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            unsafe
            {
                var questManager = QuestManager.Instance();
                return questManager != null && questManager->IsQuestAccepted(QuestId)
                    ? ETaskResult.TaskComplete
                    : ETaskResult.StillRunning;
            }
        }

        public override string ToString() => $"WaitQuestAccepted({QuestId})";
    }

    internal sealed class WaitQuestCompleted : ITask
    {
        public ushort QuestId { get; set; }

        public ITask With(ushort questId)
        {
            QuestId = questId;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            return QuestManager.IsQuestComplete(QuestId) ? ETaskResult.TaskComplete : ETaskResult.StillRunning;
        }

        public override string ToString() => $"WaitQuestComplete({QuestId})";
    }

    internal sealed class NextStep(ushort questId, int sequence) : ILastTask
    {
        public ushort QuestId { get; } = questId;
        public int Sequence { get; } = sequence;

        public bool Start() => true;

        public ETaskResult Update() => ETaskResult.NextStep;

        public override string ToString() => "NextStep";
    }

    internal sealed class EndAutomation : ILastTask
    {
        public ushort QuestId => throw new InvalidOperationException();
        public int Sequence => throw new InvalidOperationException();

        public bool Start() => true;

        public ETaskResult Update() => ETaskResult.End;

        public override string ToString() => "EndAutomation";
    }
}