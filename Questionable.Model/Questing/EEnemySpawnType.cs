﻿using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

[JsonConverter(typeof(EnemySpawnTypeConverter))]
public enum EEnemySpawnType
{
    None = 0,
    AfterInteraction,
    AfterItemUse,
    AutoOnEnterArea,
    OverworldEnemies,
}
