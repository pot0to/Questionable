﻿using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Questionable.Controller;
using Questionable.External;

namespace Questionable.Windows.QuestComponents;

internal sealed class QuickAccessButtonsComponent
{
    private readonly QuestController _questController;
    private readonly MovementController _movementController;
    private readonly GameUiController _gameUiController;
    private readonly GameFunctions _gameFunctions;
    private readonly ChatFunctions _chatFunctions;
    private readonly QuestRegistry _questRegistry;
    private readonly NavmeshIpc _navmeshIpc;
    private readonly QuestValidationWindow _questValidationWindow;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly IFramework _framework;

    public QuickAccessButtonsComponent(QuestController questController, MovementController movementController,
        GameUiController gameUiController, GameFunctions gameFunctions, ChatFunctions chatFunctions,
        QuestRegistry questRegistry, NavmeshIpc navmeshIpc, QuestValidationWindow questValidationWindow,
        IClientState clientState, ICondition condition, IFramework framework)
    {
        _questController = questController;
        _movementController = movementController;
        _gameUiController = gameUiController;
        _gameFunctions = gameFunctions;
        _chatFunctions = chatFunctions;
        _questRegistry = questRegistry;
        _navmeshIpc = navmeshIpc;
        _questValidationWindow = questValidationWindow;
        _clientState = clientState;
        _condition = condition;
        _framework = framework;
    }

    public unsafe void Draw()
    {
        var map = AgentMap.Instance();
        ImGui.BeginDisabled(map == null || map->IsFlagMarkerSet == 0 ||
                            map->FlagMapMarker.TerritoryId != _clientState.TerritoryType ||
                            !_navmeshIpc.IsReady);
        if (ImGui.Button("Move to Flag"))
        {
            _movementController.Destination = null;
            _chatFunctions.ExecuteCommand(
                $"/vnav {(_condition[ConditionFlag.Mounted] && _gameFunctions.IsFlyingUnlockedInCurrentZone() ? "flyflag" : "moveflag")}");
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(!_movementController.IsPathRunning);
        if (ImGui.Button("Stop Nav"))
        {
            _movementController.Stop();
            _questController.Stop("Manual");
        }

        ImGui.EndDisabled();

        if (ImGui.Button("Reload Data"))
        {
            _questController.Reload();
            _framework.RunOnTick(() => _gameUiController.HandleCurrentDialogueChoices(),
                TimeSpan.FromMilliseconds(200));
        }

        if (_questRegistry.ValidationIssueCount > 0)
        {
            ImGui.SameLine();
            if (DrawValidationIssuesButton())
                _questValidationWindow.IsOpen = true;
        }
    }

    private bool DrawValidationIssuesButton()
    {
        int errorCount = _questRegistry.ValidationErrorCount;
        int infoCount = _questRegistry.ValidationIssueCount - _questRegistry.ValidationErrorCount;
        if (errorCount == 0 && infoCount == 0)
            return false;

        int partsToRender = errorCount == 0 || infoCount == 0 ? 1 : 2;
        using var id = ImRaii.PushId("validationissues");

        ImGui.PushFont(UiBuilder.IconFont);
        var icon1 = FontAwesomeIcon.TimesCircle;
        var icon2 = FontAwesomeIcon.InfoCircle;
        Vector2 iconSize1 = errorCount > 0 ? ImGui.CalcTextSize(icon1.ToIconString()) : Vector2.Zero;
        Vector2 iconSize2 = infoCount > 0 ? ImGui.CalcTextSize(icon2.ToIconString()) : Vector2.Zero;
        ImGui.PopFont();

        string text1 = errorCount > 0 ? errorCount.ToString(CultureInfo.InvariantCulture) : string.Empty;
        string text2 = infoCount > 0 ? infoCount.ToString(CultureInfo.InvariantCulture) : string.Empty;
        Vector2 textSize1 = errorCount > 0 ? ImGui.CalcTextSize(text1) : Vector2.Zero;
        Vector2 textSize2 = infoCount > 0 ? ImGui.CalcTextSize(text2) : Vector2.Zero;
        var dl = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();

        var iconPadding = 3 * ImGuiHelpers.GlobalScale;

        // Draw an ImGui button with the icon and text
        var buttonWidth = iconSize1.X + iconSize2.X + textSize1.X + textSize2.X +
                          (ImGui.GetStyle().FramePadding.X * 2) + iconPadding * 2 * partsToRender;
        var buttonHeight = ImGui.GetFrameHeight();
        var button = ImGui.Button(string.Empty, new Vector2(buttonWidth, buttonHeight));

        // Draw the icon on the window drawlist
        Vector2 position = new Vector2(cursor.X + ImGui.GetStyle().FramePadding.X,
            cursor.Y + ImGui.GetStyle().FramePadding.Y);
        if (errorCount > 0)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            dl.AddText(position, ImGui.GetColorU32(ImGuiColors.DalamudRed), icon1.ToIconString());
            ImGui.PopFont();
            position = position with { X = position.X + iconSize1.X + iconPadding };

            // Draw the text on the window drawlist
            dl.AddText(position, ImGui.GetColorU32(ImGuiCol.Text), text1);
            position = position with { X = position.X + textSize1.X + 2 * iconPadding };
        }

        if (infoCount > 0)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            dl.AddText(position, ImGui.GetColorU32(ImGuiColors.ParsedBlue), icon2.ToIconString());
            ImGui.PopFont();
            position = position with { X = position.X + iconSize2.X + iconPadding };

            // Draw the text on the window drawlist
            dl.AddText(position, ImGui.GetColorU32(ImGuiCol.Text), text2);
        }

        return button;
    }
}