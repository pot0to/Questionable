﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using Json.Schema;
using Questionable.Model;
using Questionable.Model.Questing;
using Questionable.QuestPaths;

namespace Questionable.Validation.Validators;

internal sealed class JsonSchemaValidator : IQuestValidator
{
    private readonly Dictionary<ElementId, JsonNode> _questNodes = new();
    private JsonSchema? _questSchema;

    public JsonSchemaValidator()
    {
        SchemaRegistry.Global.Register(
            new Uri("https://git.carvel.li/liza/Questionable/raw/branch/master/Questionable.Model/common-schema.json"),
            JsonSchema.FromStream(AssemblyModelLoader.CommonSchema).AsTask().Result);
    }

    public IEnumerable<ValidationIssue> Validate(Quest quest)
    {
        _questSchema ??= JsonSchema.FromStream(AssemblyQuestLoader.QuestSchema).AsTask().Result;

        if (_questNodes.TryGetValue(quest.Id, out JsonNode? questNode))
        {
            var evaluationResult = _questSchema.Evaluate(questNode, new EvaluationOptions
            {
                Culture = CultureInfo.InvariantCulture,
                OutputFormat = OutputFormat.List
            });
            if (!evaluationResult.IsValid)
            {
                yield return new ValidationIssue
                {
                    ElementId = quest.Id,
                    Sequence = null,
                    Step = null,
                    Type = EIssueType.InvalidJsonSchema,
                    Severity = EIssueSeverity.Error,
                    Description = "JSON Validation failed"
                };
            }
        }
    }

    public void Enqueue(ElementId elementId, JsonNode questNode) => _questNodes[elementId] = questNode;

    public void Reset() => _questNodes.Clear();
}
