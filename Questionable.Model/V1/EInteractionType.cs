﻿using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(InteractionTypeConverter))]
public enum EInteractionType
{
    Interact,
    WalkTo,
    AttuneAethernetShard,
    AttuneAetheryte,
    AttuneAetherCurrent,
    Combat,
    UseItem,
    EquipItem,
    Say,
    Emote,
    Action,
    WaitForObjectAtPosition,
    WaitForManualProgress,
    Duty,
    SinglePlayerDuty,
    Jump,
    Dive,

    /// <summary>
    /// Needs to be manually continued.
    /// </summary>
    Instruction,

    AcceptQuest,
    CompleteQuest,
}
