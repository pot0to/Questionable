﻿using System.Collections.Generic;
using LLib.GameData;
using Lumina.Excel.GeneratedSheets;
using Questionable.Model.Questing;

namespace Questionable.Model;

internal sealed class LeveInfo : IQuestInfo
{
    public LeveInfo(Leve leve)
    {
        QuestId = new LeveId((ushort)leve.RowId);
        Name = leve.Name;
        Level = leve.ClassJobLevel;
        IssuerDataId = leve.LevelLevemete.Value!.Object;
        ClassJobs = QuestInfoUtils.AsList(leve.ClassJobCategory.Value!);
    }

    public ElementId QuestId { get; }
    public string Name { get; }
    public uint IssuerDataId { get; }
    public bool IsRepeatable => true;
    public ushort Level { get; }
    public EBeastTribe BeastTribe => EBeastTribe.None;
    public bool IsMainScenarioQuest => false;
    public IReadOnlyList<EClassJob> ClassJobs { get; }
}
