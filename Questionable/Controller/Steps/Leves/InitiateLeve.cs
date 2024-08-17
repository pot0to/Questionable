﻿using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Model;
using Questionable.Model.Questing;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Questionable.Controller.Steps.Leves;

internal static class InitiateLeve
{
    internal sealed class Factory(IServiceProvider serviceProvider, ICondition condition) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.InitiateLeve)
                yield break;

            yield return serviceProvider.GetRequiredService<SkipInitiateIfActive>().With(quest.Id);
            yield return serviceProvider.GetRequiredService<OpenJournal>().With(quest.Id);
            yield return serviceProvider.GetRequiredService<Initiate>().With(quest.Id);
            yield return serviceProvider.GetRequiredService<SelectDifficulty>();
            yield return new WaitConditionTask(() => condition[ConditionFlag.BoundByDuty], "Wait(BoundByDuty)");
        }
    }

    internal sealed unsafe class SkipInitiateIfActive : ITask
    {
        private ElementId _elementId = null!;

        public ITask With(ElementId elementId)
        {
            _elementId = elementId;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            var director = UIState.Instance()->DirectorTodo.Director;
            if (director != null &&
                director->EventHandlerInfo != null &&
                director->EventHandlerInfo->EventId.ContentId == EventHandlerType.GatheringLeveDirector &&
                director->ContentId == _elementId.Value)
                return ETaskResult.SkipRemainingTasksForStep;

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"CheckIfAlreadyActive({_elementId})";
    }

    internal sealed unsafe class OpenJournal : ITask
    {
        private ElementId _elementId = null!;
        private uint _questType;
        private DateTime _openedAt = DateTime.MinValue;

        public ITask With(ElementId elementId)
        {
            _elementId = elementId;
            _questType = _elementId is LeveId ? 2u : 1u;
            return this;
        }

        public bool Start()
        {
            AgentQuestJournal.Instance()->OpenForQuest(_elementId.Value, _questType);
            _openedAt = DateTime.Now;
            return true;
        }

        public ETaskResult Update()
        {
            AgentQuestJournal* agentQuestJournal = AgentQuestJournal.Instance();
            if (agentQuestJournal->IsAgentActive() &&
                agentQuestJournal->SelectedQuestId == _elementId.Value &&
                agentQuestJournal->SelectedQuestType == _questType)
                return ETaskResult.TaskComplete;

            if (DateTime.Now > _openedAt.AddSeconds(3))
            {
                AgentQuestJournal.Instance()->OpenForQuest(_elementId.Value, _questType);
                _openedAt = DateTime.Now;
            }

            return ETaskResult.StillRunning;
        }

        public override string ToString() => $"OpenJournal({_elementId})";
    }

    internal sealed unsafe class Initiate(IGameGui gameGui) : ITask
    {
        private ElementId _elementId = null!;

        public ITask With(ElementId elementId)
        {
            _elementId = elementId;
            return this;
        }

        public bool Start() => true;

        public ETaskResult Update()
        {
            if (gameGui.TryGetAddonByName("JournalDetail", out AtkUnitBase* addonJournalDetail))
            {
                var pickQuest = stackalloc AtkValue[]
                {
                    new() { Type = ValueType.Int, Int = 4 },
                    new() { Type = ValueType.UInt, Int = _elementId.Value }
                };
                addonJournalDetail->FireCallback(2, pickQuest);
                return ETaskResult.TaskComplete;
            }

            return ETaskResult.StillRunning;
        }

        public override string ToString() => $"InitiateLeve({_elementId})";
    }

    internal sealed unsafe class SelectDifficulty(IGameGui gameGui) : ITask
    {
        public bool Start() => true;

        public ETaskResult Update()
        {
            if (gameGui.TryGetAddonByName("GuildLeveDifficulty", out AtkUnitBase* addon))
            {
                // atkvalues: 1 → default difficulty, 2 → min, 3 → max


                var pickDifficulty = stackalloc AtkValue[]
                {
                    new() { Type = ValueType.Int, Int = 0 },
                    new() { Type = ValueType.Int, Int = addon->AtkValues[1].Int }
                };
                addon->FireCallback(2, pickDifficulty, true);
                return ETaskResult.TaskComplete;
            }

            return ETaskResult.StillRunning;
        }

        public override string ToString() => "SelectLeveDifficulty";
    }
}
