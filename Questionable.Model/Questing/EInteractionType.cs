﻿using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

[JsonConverter(typeof(InteractionTypeConverter))]
public enum EInteractionType
{
    None,
    Interact,
    WalkTo,
    AttuneAethernetShard,
    AttuneAetheryte,
    AttuneAetherCurrent,
    Combat,
    UseItem,
    EquipItem,
    EquipRecommended,
    Say,
    Emote,
    Action,
    WaitForObjectAtPosition,
    WaitForManualProgress,
    Duty,
    SinglePlayerDuty,
    Jump,
    Dive,
    Craft,

    /// <summary>
    /// Needs to be manually continued.
    /// </summary>
    Instruction,

    AcceptQuest,
    CompleteQuest,
    AcceptLeve,
    InitiateLeve,
    CompleteLeve,

    // unmapped extra types below
    InternalGather,
}
