﻿using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

public sealed class DialogueChoice
{
    [JsonConverter(typeof(DialogueChoiceTypeConverter))]
    public EDialogChoiceType Type { get; set; }
    public string? ExcelSheet { get; set; }

    [JsonConverter(typeof(ExcelRefConverter))]
    public ExcelRef? Prompt { get; set; }

    public bool Yes { get; set; } = true;

    [JsonConverter(typeof(ExcelRefConverter))]
    public ExcelRef? Answer { get; set; }

    /// <summary>
    /// If set, only applies when focusing the given target id.
    /// </summary>
    public uint? DataId { get; set; }
}