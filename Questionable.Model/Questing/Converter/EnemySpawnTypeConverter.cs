﻿using System.Collections.Generic;
using Questionable.Model.Common.Converter;

namespace Questionable.Model.Questing.Converter;

public sealed class EnemySpawnTypeConverter() : EnumConverter<EEnemySpawnType>(Values)
{
    private static readonly Dictionary<EEnemySpawnType, string> Values = new()
    {
        { EEnemySpawnType.AfterInteraction, "AfterInteraction" },
        { EEnemySpawnType.AfterItemUse, "AfterItemUse" },
        { EEnemySpawnType.AutoOnEnterArea, "AutoOnEnterArea" },
        { EEnemySpawnType.OverworldEnemies, "OverworldEnemies" },
    };
}