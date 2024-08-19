﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using LLib;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;
using Action = System.Action;
using Quest = Questionable.Model.Quest;

namespace Questionable.Controller.Steps.Shared;

internal static class Move
{
    internal sealed class Factory(IServiceProvider serviceProvider, AetheryteData aetheryteData) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.Position != null)
            {
                var builder = serviceProvider.GetRequiredService<MoveBuilder>()
                    .With(quest.Id, step, step.Position.Value);
                return builder.Build();
            }
            else if (step is { DataId: not null, StopDistance: not null })
            {
                var task = serviceProvider.GetRequiredService<ExpectToBeNearDataId>();
                task.DataId = step.DataId.Value;
                task.StopDistance = step.StopDistance.Value;
                return [task];
            }
            else if (step is { InteractionType: EInteractionType.AttuneAetheryte, Aetheryte: not null })
            {
                var builder = serviceProvider.GetRequiredService<MoveBuilder>()
                    .With(quest.Id, step, aetheryteData.Locations[step.Aetheryte.Value]);
                return builder.Build();
            }
            else if (step is { InteractionType: EInteractionType.AttuneAethernetShard, AethernetShard: not null })
            {
                var builder = serviceProvider.GetRequiredService<MoveBuilder>().With(quest.Id, step,
                    aetheryteData.Locations[step.AethernetShard.Value]);
                return builder.Build();
            }

            return [];
        }
    }

    internal sealed class MoveBuilder(
        IServiceProvider serviceProvider,
        ILogger<MoveBuilder> logger,
        GameFunctions gameFunctions,
        IClientState clientState,
        MovementController movementController,
        TerritoryData territoryData,
        AetheryteData aetheryteData)
    {
        private ElementId _questId = null!;
        private QuestStep _step = null!;
        private Vector3 _destination;

        public MoveBuilder With(ElementId questId, QuestStep step, Vector3 destination)
        {
            _questId = questId;
            _step = step;
            _destination = destination;
            return this;
        }

        public IEnumerable<ITask> Build()
        {
            if (_step.InteractionType == EInteractionType.Jump && _step.JumpDestination != null &&
                (clientState.LocalPlayer!.Position - _step.JumpDestination.Position).Length() <=
                (_step.JumpDestination.StopDistance ?? 1f))
            {
                logger.LogInformation("We're at the jump destination, skipping movement");
                yield break;
            }

            yield return new WaitConditionTask(() => clientState.TerritoryType == _step.TerritoryId,
                $"Wait(territory: {territoryData.GetNameAndId(_step.TerritoryId)})");

            if (!_step.DisableNavmesh)
                yield return new WaitConditionTask(() => movementController.IsNavmeshReady,
                    "Wait(navmesh ready)");

            float stopDistance = _step.CalculateActualStopDistance();
            Vector3? position = clientState.LocalPlayer?.Position;
            float actualDistance = position == null ? float.MaxValue : Vector3.Distance(position.Value, _destination);

            // if we teleport to a different zone, assume we always need to move; this is primarily relevant for cases
            // where you're e.g. in Lakeland, and the step navigates via Crystarium → Tesselation back into the same
            // zone.
            //
            // Side effects of this check being broken include:
            //   - mounting when near the target npc (if you spawn close enough for the next step)
            //   - trying to fly when near the target npc (if close enough where no movement is required)
            if (_step.AetheryteShortcut != null &&
                aetheryteData.TerritoryIds[_step.AetheryteShortcut.Value] != _step.TerritoryId)
            {
                logger.LogDebug("Aetheryte: Changing distance to max, previous distance: {Distance}", actualDistance);
                actualDistance = float.MaxValue;
            }

            // In particular, MoveBuilder is used so early that it'll have the position when you're starting gathering,
            // not when you're finished.
            if (_questId is SatisfactionSupplyNpcId)
            {
                logger.LogDebug("SatisfactionSupply: Changing distance to max, previous distance: {Distance}",
                    actualDistance);
                actualDistance = float.MaxValue;
            }

            if (_step.Mount == true)
                yield return serviceProvider.GetRequiredService<MountTask>()
                    .With(_step.TerritoryId, MountTask.EMountIf.Always);
            else if (_step.Mount == false)
                yield return serviceProvider.GetRequiredService<UnmountTask>();

            if (!_step.DisableNavmesh)
            {
                if (_step.Mount == null)
                {
                    MountTask.EMountIf mountIf =
                        actualDistance > stopDistance && _step.Fly == true &&
                        gameFunctions.IsFlyingUnlocked(_step.TerritoryId)
                            ? MountTask.EMountIf.Always
                            : MountTask.EMountIf.AwayFromPosition;
                    yield return serviceProvider.GetRequiredService<MountTask>()
                        .With(_step.TerritoryId, mountIf, _destination);
                }

                if (actualDistance > stopDistance)
                {
                    yield return serviceProvider.GetRequiredService<MoveInternal>()
                        .With(_step, _destination);
                }
                else
                    logger.LogInformation("Skipping move task, distance: {ActualDistance} < {StopDistance}",
                        actualDistance, stopDistance);
            }
            else
            {
                // navmesh won't move close enough
                if (actualDistance > stopDistance)
                {
                    yield return serviceProvider.GetRequiredService<MoveInternal>()
                        .With(_step, _destination);
                }
                else
                    logger.LogInformation("Skipping move task, distance: {ActualDistance} < {StopDistance}",
                        actualDistance, stopDistance);
            }

            if (_step.Fly == true && _step.Land == true)
                yield return serviceProvider.GetRequiredService<Land>();
        }
    }

    internal sealed class MoveInternal(
        MovementController movementController,
        GameFunctions gameFunctions,
        ILogger<MoveInternal> logger,
        ICondition condition,
        IDataManager dataManager) : ITask, IToastAware
    {
        private string _cannotExecuteAtThisTime = dataManager.GetString<LogMessage>(579, x => x.Text)!;

        public Action StartAction { get; set; } = null!;
        public Vector3 Destination { get; set; }

        public ITask With(QuestStep step, Vector3 destination)
        {
            return With(
                territoryId: step.TerritoryId,
                destination: destination,
                stopDistance: step.CalculateActualStopDistance(),
                dataId: step.DataId,
                disableNavMesh: step.DisableNavmesh,
                sprint: step.Sprint != false,
                fly: step.Fly == true,
                land: step.Land == true,
                ignoreDistanceToObject: step.IgnoreDistanceToObject == true);
        }

        public ITask With(ushort territoryId, Vector3 destination, float? stopDistance = null, uint? dataId = null,
            bool disableNavMesh = false, bool sprint = true, bool fly = false, bool land = false,
            bool ignoreDistanceToObject = false)
        {
            Destination = destination;

            if (!gameFunctions.IsFlyingUnlocked(territoryId))
            {
                fly = false;
                land = false;
            }

            if (!disableNavMesh)
            {
                StartAction = () =>
                    movementController.NavigateTo(EMovementType.Quest, dataId, Destination,
                        fly: fly,
                        sprint: sprint,
                        stopDistance: stopDistance,
                        ignoreDistanceToObject: ignoreDistanceToObject,
                        land: land);
            }
            else
            {
                StartAction = () =>
                    movementController.NavigateTo(EMovementType.Quest, dataId, [Destination],
                        fly: fly,
                        sprint: sprint,
                        stopDistance: stopDistance,
                        ignoreDistanceToObject: ignoreDistanceToObject,
                        land: land);
            }

            return this;
        }

        public bool Start()
        {
            logger.LogInformation("Moving to {Destination}", Destination.ToString("G", CultureInfo.InvariantCulture));
            StartAction();
            return true;
        }

        public ETaskResult Update()
        {
            if (movementController.IsPathfinding || movementController.IsPathRunning)
                return ETaskResult.StillRunning;

            DateTime movementStartedAt = movementController.MovementStartedAt;
            if (movementStartedAt == DateTime.MaxValue || movementStartedAt.AddSeconds(2) >= DateTime.Now)
                return ETaskResult.StillRunning;

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"MoveTo({Destination.ToString("G", CultureInfo.InvariantCulture)})";

        public bool OnErrorToast(SeString message)
        {
            if (GameFunctions.GameStringEquals(_cannotExecuteAtThisTime, message.TextValue) &&
                condition[ConditionFlag.Diving])
                return true;

            return false;
        }
    }

    internal sealed class ExpectToBeNearDataId(GameFunctions gameFunctions, IClientState clientState) : ITask
    {
        public uint DataId { get; set; }
        public float StopDistance { get; set; }

        public bool Start() => true;

        public ETaskResult Update()
        {
            IGameObject? gameObject = gameFunctions.FindObjectByDataId(DataId);
            if (gameObject == null ||
                (gameObject.Position - clientState.LocalPlayer!.Position).Length() > StopDistance)
            {
                throw new TaskException("Object not found or too far away, no position so we can't move");
            }

            return ETaskResult.TaskComplete;
        }
    }

    internal sealed class Land(IClientState clientState, ICondition condition, ILogger<Land> logger) : ITask
    {
        private bool _landing;
        private DateTime _continueAt;

        public bool Start()
        {
            if (!condition[ConditionFlag.InFlight])
            {
                logger.LogInformation("Not flying, not attempting to land");
                return false;
            }

            _landing = AttemptLanding();
            _continueAt = DateTime.Now.AddSeconds(0.25);
            return true;
        }

        public ETaskResult Update()
        {
            if (DateTime.Now < _continueAt)
                return ETaskResult.StillRunning;

            if (condition[ConditionFlag.InFlight])
            {
                if (!_landing)
                {
                    _landing = AttemptLanding();
                    _continueAt = DateTime.Now.AddSeconds(0.25);
                }

                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        private unsafe bool AttemptLanding()
        {
            var character = (Character*)(clientState.LocalPlayer?.Address ?? 0);
            if (character != null)
            {
                if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 23) == 0)
                {
                    logger.LogInformation("Attempting to land");
                    return ActionManager.Instance()->UseAction(ActionType.GeneralAction, 23);
                }
            }

            return false;
        }
    }
}
