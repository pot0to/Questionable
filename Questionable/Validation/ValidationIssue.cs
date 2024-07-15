﻿namespace Questionable.Validation;

internal sealed record ValidationIssue
{
    public required ushort QuestId { get; init; }
    public required byte? Sequence { get; init; }
    public required int? Step { get; init; }
    public required EIssueSeverity Severity { get; init; }
    public required string Description { get; init; }
}