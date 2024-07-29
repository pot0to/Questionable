﻿using System.Collections.Generic;
using System.Linq;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Validation.Validators;

internal sealed class AethernetShortcutValidator : IQuestValidator
{
    private readonly AetheryteData _aetheryteData;

    public AethernetShortcutValidator(AetheryteData aetheryteData)
    {
        _aetheryteData = aetheryteData;
    }

    public IEnumerable<ValidationIssue> Validate(Quest quest)
    {
        return quest.AllSteps()
            .Select(x => Validate(quest.QuestId, x.Sequence.Sequence, x.StepId, x.Step.AethernetShortcut))
            .Where(x => x != null)
            .Cast<ValidationIssue>();
    }

    private ValidationIssue? Validate(ushort questId, int sequenceNo, int stepId, AethernetShortcut? aethernetShortcut)
    {
        if (aethernetShortcut == null)
            return null;

        byte fromGroup = _aetheryteData.AethernetGroups.GetValueOrDefault(aethernetShortcut.From);
        byte toGroup = _aetheryteData.AethernetGroups.GetValueOrDefault(aethernetShortcut.To);
        if (fromGroup != toGroup)
        {
            return new ValidationIssue
            {
                QuestId = questId,
                Sequence = (byte)sequenceNo,
                Step = stepId,
                Type = EIssueType.InvalidAethernetShortcut,
                Severity = EIssueSeverity.Error,
                Description = $"Invalid aethernet shortcut: {aethernetShortcut.From} to {aethernetShortcut.To}"
            };
        }

        return null;
    }
}