﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using Lumina.Excel.GeneratedSheets;
using Questionable.Model.V1;
using Quest = Questionable.Model.Quest;

namespace Questionable.Controller;

internal sealed class GameUiController : IDisposable
{
    private readonly IClientState _clientState;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IDataManager _dataManager;
    private readonly GameFunctions _gameFunctions;
    private readonly QuestController _questController;
    private readonly IGameGui _gameGui;
    private readonly IPluginLog _pluginLog;

    public GameUiController(IClientState clientState, IAddonLifecycle addonLifecycle, IDataManager dataManager,
        GameFunctions gameFunctions, QuestController questController, IGameGui gameGui, IPluginLog pluginLog)
    {
        _clientState = clientState;
        _addonLifecycle = addonLifecycle;
        _dataManager = dataManager;
        _gameFunctions = gameFunctions;
        _questController = questController;
        _gameGui = gameGui;
        _pluginLog = pluginLog;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectString", SelectStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "CutSceneSelectString", CutsceneSelectStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectIconString", SelectIconStringPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesnoPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Credit", CreditPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
    }

    internal unsafe void HandleCurrentDialogueChoices()
    {
        if (_gameGui.TryGetAddonByName("SelectString", out AddonSelectString* addonSelectString))
        {
            _pluginLog.Information("SelectString window is open");
            SelectStringPostSetup(addonSelectString);
        }

        if (_gameGui.TryGetAddonByName("CutSceneSelectString",
                out AddonCutSceneSelectString* addonCutSceneSelectString))
        {
            _pluginLog.Information("CutSceneSelectString window is open");
            CutsceneSelectStringPostSetup(addonCutSceneSelectString);
        }

        if (_gameGui.TryGetAddonByName("SelectIconString", out AddonSelectIconString* addonSelectIconString))
        {
            _pluginLog.Information("SelectIconString window is open");
            SelectIconStringPostSetup(addonSelectIconString);
        }

        if (_gameGui.TryGetAddonByName("SelectYesno", out AddonSelectYesno* addonSelectYesno))
        {
            _pluginLog.Information("SelectYesno window is open");
            SelectYesnoPostSetup(addonSelectYesno);
        }
    }

    private unsafe void SelectStringPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonSelectString* addonSelectString = (AddonSelectString*)args.Addon;
        SelectStringPostSetup(addonSelectString);
    }

    private unsafe void SelectStringPostSetup(AddonSelectString* addonSelectString)
    {
        string? actualPrompt = addonSelectString->AtkUnitBase.AtkValues[2].ReadAtkString();
        if (actualPrompt == null)
            return;

        List<string?> answers = new();
        for (ushort i = 7; i < addonSelectString->AtkUnitBase.AtkValuesCount; ++i)
            answers.Add(addonSelectString->AtkUnitBase.AtkValues[i].ReadAtkString());

        int? answer = HandleListChoice(actualPrompt, answers);
        if (answer != null)
        {
            _questController.IncreaseDialogueChoicesSelected();
            addonSelectString->AtkUnitBase.FireCallbackInt(answer.Value);
        }
    }

    private unsafe void CutsceneSelectStringPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonCutSceneSelectString* addonCutSceneSelectString = (AddonCutSceneSelectString*)args.Addon;
        CutsceneSelectStringPostSetup(addonCutSceneSelectString);
    }

    private unsafe void CutsceneSelectStringPostSetup(AddonCutSceneSelectString* addonCutSceneSelectString)
    {
        string? actualPrompt = addonCutSceneSelectString->AtkUnitBase.AtkValues[2].ReadAtkString();
        if (actualPrompt == null)
            return;

        List<string?> answers = new();
        for (int i = 5; i < addonCutSceneSelectString->AtkUnitBase.AtkValuesCount; ++i)
            answers.Add(addonCutSceneSelectString->AtkUnitBase.AtkValues[i].ReadAtkString());

        int? answer = HandleListChoice(actualPrompt, answers);
        if (answer != null)
        {
            _questController.IncreaseDialogueChoicesSelected();
            addonCutSceneSelectString->AtkUnitBase.FireCallbackInt(answer.Value);
        }
    }

    private unsafe void SelectIconStringPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonSelectIconString* addonSelectIconString = (AddonSelectIconString*)args.Addon;
        SelectIconStringPostSetup(addonSelectIconString);
    }

    private unsafe void SelectIconStringPostSetup(AddonSelectIconString* addonSelectIconString)
    {
        string? actualPrompt = addonSelectIconString->AtkUnitBase.AtkValues[3].ReadAtkString();
        if (string.IsNullOrEmpty(actualPrompt))
            actualPrompt = null;

        List<string?> answers = new();
        for (ushort i = 0; i < addonSelectIconString->AtkUnitBase.AtkValues[5].Int; i++)
            answers.Add(addonSelectIconString->AtkUnitBase.AtkValues[i * 3 + 7].ReadAtkString());

        int? answer = HandleListChoice(actualPrompt, answers);
        if (answer != null)
        {
            _questController.IncreaseDialogueChoicesSelected();
            addonSelectIconString->AtkUnitBase.FireCallbackInt(answer.Value);
        }
    }


    private int? HandleListChoice(string? actualPrompt, List<string?> answers)
    {
        var currentQuest = _questController.CurrentQuest;
        if (currentQuest == null)
        {
            _pluginLog.Information("Ignoring list choice, no active quest");
            return null;
        }

        var quest = currentQuest.Quest;
        var step = quest.FindSequence(currentQuest.Sequence)?.FindStep(currentQuest.Step);
        if (step == null)
        {
            _pluginLog.Information("Ignoring list choice, no active step");
            return null;
        }

        foreach (var dialogueChoice in step.DialogueChoices)
        {
            if (dialogueChoice.Answer == null)
            {
                _pluginLog.Information("Ignoring entry in DialogueChoices, no answer");
                continue;
            }

            string? excelPrompt = null, excelAnswer;
            switch (dialogueChoice.Type)
            {
                case EDialogChoiceType.ContentTalkList:
                    if (dialogueChoice.Prompt != null)
                    {
                        excelPrompt =
                            _gameFunctions.GetContentTalk(uint.Parse(dialogueChoice.Prompt,
                                CultureInfo.InvariantCulture));
                    }

                    excelAnswer =
                        _gameFunctions.GetContentTalk(uint.Parse(dialogueChoice.Answer, CultureInfo.InvariantCulture));
                    break;
                case EDialogChoiceType.List:
                    if (dialogueChoice.Prompt != null)
                    {
                        excelPrompt =
                            _gameFunctions.GetDialogueText(quest, dialogueChoice.ExcelSheet, dialogueChoice.Prompt);
                    }

                    excelAnswer =
                        _gameFunctions.GetDialogueText(quest, dialogueChoice.ExcelSheet, dialogueChoice.Answer);
                    break;
                default:
                    continue;
            }

            if (actualPrompt == null && !string.IsNullOrEmpty(excelPrompt))
            {
                _pluginLog.Information($"Unexpected excelPrompt: {excelPrompt}");
                continue;
            }

            if (actualPrompt != null && (excelPrompt == null || !GameStringEquals(actualPrompt, excelPrompt)))
            {
                _pluginLog.Information($"Unexpected excelPrompt: {excelPrompt}, actualPrompt: {actualPrompt}");
                continue;
            }

            for (int i = 0; i < answers.Count; ++i)
            {
                if (GameStringEquals(answers[i], excelAnswer))
                {
                    _pluginLog.Information($"Returning {i}: '{answers[i]}' for '{actualPrompt}'");
                    return i;
                }
            }
        }

        _pluginLog.Information($"No matching answer found for {actualPrompt}.");
        return null;
    }

    private unsafe void SelectYesnoPostSetup(AddonEvent type, AddonArgs args)
    {
        AddonSelectYesno* addonSelectYesno = (AddonSelectYesno*)args.Addon;
        SelectYesnoPostSetup(addonSelectYesno);
    }

    private unsafe void SelectYesnoPostSetup(AddonSelectYesno* addonSelectYesno)
    {
        string? actualPrompt = addonSelectYesno->AtkUnitBase.AtkValues[0].ReadAtkString();
        if (actualPrompt == null)
            return;

        _pluginLog.Verbose($"Prompt: '{actualPrompt}'");

        var currentQuest = _questController.CurrentQuest;
        if (currentQuest == null)
            return;

        var quest = currentQuest.Quest;
        var step = quest.FindSequence(currentQuest.Sequence)?.FindStep(currentQuest.Step);
        if (step != null && HandleDefaultYesNo(addonSelectYesno, quest, step, actualPrompt))
            return;

        HandleTravelYesNo(addonSelectYesno, currentQuest, actualPrompt);
    }

    private unsafe bool HandleDefaultYesNo(AddonSelectYesno* addonSelectYesno, Quest quest, QuestStep step,
        string actualPrompt)
    {
        _pluginLog.Verbose($"DefaultYesNo: Choice count: {step.DialogueChoices.Count}");
        foreach (var dialogueChoice in step.DialogueChoices)
        {
            string? excelPrompt;
            switch (dialogueChoice.Type)
            {
                case EDialogChoiceType.ContentTalkYesNo:
                    excelPrompt =
                        _gameFunctions.GetContentTalk(uint.Parse(dialogueChoice.Prompt, CultureInfo.InvariantCulture));
                    break;
                case EDialogChoiceType.YesNo:
                    excelPrompt =
                        _gameFunctions.GetDialogueText(quest, dialogueChoice.ExcelSheet, dialogueChoice.Prompt);
                    break;
                default:
                    continue;
            }

            if (excelPrompt == null || !GameStringEquals(actualPrompt, excelPrompt))
                continue;

            addonSelectYesno->AtkUnitBase.FireCallbackInt(dialogueChoice.Yes ? 0 : 1);
            _questController.IncreaseDialogueChoicesSelected();
            return true;
        }

        return false;
    }

    private unsafe bool HandleTravelYesNo(AddonSelectYesno* addonSelectYesno,
        QuestController.QuestProgress currentQuest, string actualPrompt)
    {
        // this can be triggered either manually (in which case we should increase the step counter), or automatically
        // (in which case it is ~1 frame later, and the step counter has already been increased)
        var sequence = currentQuest.Quest.FindSequence(currentQuest.Sequence);
        if (sequence == null)
            return false;

        bool increaseStepCount = true;
        QuestStep? step = sequence.FindStep(currentQuest.Step);
        if (step != null)
            _pluginLog.Verbose($"Current step: {step.TerritoryId}, {step.TargetTerritoryId}");

        if (step == null || step.TargetTerritoryId == null)
        {
            _pluginLog.Verbose("TravelYesNo: Checking previous step...");
            step = sequence.FindStep(currentQuest.Step == 255 ? (sequence.Steps.Count - 1) : (currentQuest.Step - 1));
            increaseStepCount = false;

            if (step != null)
                _pluginLog.Verbose($"Previous step: {step.TerritoryId}, {step.TargetTerritoryId}");
        }

        if (step == null || step.TargetTerritoryId == null)
        {
            _pluginLog.Verbose("TravelYesNo: Not found");
            return false;
        }

        var warps = _dataManager.GetExcelSheet<Warp>()!
            .Where(x => x.RowId > 0 && x.TerritoryType.Row == step.TargetTerritoryId)
            .Where(x => x.ConfirmEvent.Row == 0); // unsure if this is needed
        foreach (var entry in warps)
        {
            string? excelPrompt = entry.Question?.ToString();
            if (excelPrompt == null || !GameStringEquals(excelPrompt, actualPrompt))
            {
                _pluginLog.Information($"Ignoring prompt '{excelPrompt}'");
                continue;
            }

            _pluginLog.Information($"Using warp {entry.RowId}, {excelPrompt}");
            addonSelectYesno->AtkUnitBase.FireCallbackInt(0);
            if (increaseStepCount)
                _questController.IncreaseStepCount();
            return true;
        }

        return false;
    }

    private unsafe void CreditPostSetup(AddonEvent type, AddonArgs args)
    {
        _pluginLog.Information("Closing Credits sequence");
        AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
        addon->FireCallbackInt(-2);
    }

    private unsafe void UnendingCodexPostSetup(AddonEvent type, AddonArgs args)
    {
        if (_questController.CurrentQuest?.Quest.QuestId == 4526)
        {
            _pluginLog.Information("Closing Unending Codex");
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            addon->FireCallbackInt(-2);
        }
    }

    /// <summary>
    /// Ensures characters like '-' are handled equally in both strings.
    /// </summary>
    private bool GameStringEquals(string? a, string? b)
    {
        if (a == null)
            return b == null;

        if (b == null)
            return false;

        return a.ReplaceLineEndings().Replace('\u2013', '-') == b.ReplaceLineEndings().Replace('\u2013', '-');
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "AkatsukiNote", UnendingCodexPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Credit", CreditPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesnoPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectIconString", SelectIconStringPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "CutSceneSelectString", CutsceneSelectStringPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectString", SelectStringPostSetup);
    }
}
