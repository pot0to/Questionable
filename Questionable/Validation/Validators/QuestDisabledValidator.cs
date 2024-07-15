﻿using System.Collections.Generic;
using Questionable.Model;

namespace Questionable.Validation.Validators;

internal sealed class QuestDisabledValidator : IQuestValidator
{
    public IEnumerable<ValidationIssue> Validate(Quest quest)
    {
        if (quest.Root.Disabled)
        {
            yield return new ValidationIssue
            {
                QuestId = quest.QuestId,
                Sequence = null,
                Step = null,
                Severity = EIssueSeverity.None,
                Description = "Quest is disabled",
            };
        }
    }
}