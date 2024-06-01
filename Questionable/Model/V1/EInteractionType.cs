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
    Say,
    Emote,
    WaitForObjectAtPosition,
    WaitForManualProgress,
    Duty,
    SinglePlayerDuty,
    Jump,
    CutsceneSelectString,

    /// <summary>
    /// Needs to be adjusted for coords etc. in the quest data.
    /// </summary>
    ShouldBeAJump,

    /// <summary>
    /// Needs to be manually continued.
    /// </summary>
    Instruction,
}
