﻿using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;
using Questionable.Model;

namespace Questionable.Data;

internal sealed class JournalData
{
    public JournalData(IDataManager dataManager, QuestData questData)
    {
        var genres = dataManager.GetExcelSheet<JournalGenre>()!
            .Where(x => x.RowId > 0 && x.Icon > 0)
            .Select(x => new Genre(x, questData.GetAllByJournalGenre(x.RowId)))
            .ToList();

        var limsaStart = dataManager.GetExcelSheet<QuestRedo>()!.GetRow(1)!;
        var gridaniaStart = dataManager.GetExcelSheet<QuestRedo>()!.GetRow(2)!;
        var uldahStart = dataManager.GetExcelSheet<QuestRedo>()!.GetRow(3)!;
        var genreLimsa = new Genre(uint.MaxValue - 3, "Starting in Limsa Lominsa", 1,
            new uint[] { 108, 109 }.Concat(limsaStart.Quest.Select(x => x.Row))
                .Where(x => x != 0)
                .Select(x => questData.GetQuestInfo((ushort)(x & 0xFFFF))).ToList());
        var genreGridania = new Genre(uint.MaxValue - 2, "Starting in Gridania", 1,
            new uint[] { 85, 123, 124 }.Concat(gridaniaStart.Quest.Select(x => x.Row))
                .Where(x => x != 0)
                .Select(x => questData.GetQuestInfo((ushort)(x & 0xFFFF))).ToList());
        var genreUldah = new Genre(uint.MaxValue - 1, "Starting in Ul'dah", 1,
            new uint[] { 568, 569, 570 }.Concat(uldahStart.Quest.Select(x => x.Row))
                .Where(x => x != 0)
                .Select(x => questData.GetQuestInfo((ushort)(x & 0xFFFF)))
                .ToList());
        genres.InsertRange(0, [genreLimsa, genreGridania, genreUldah]);
        genres.Single(x => x.Id == 1)
            .Quests
            .RemoveAll(x =>
                genreLimsa.Quests.Contains(x) || genreGridania.Quests.Contains(x) || genreUldah.Quests.Contains(x));

        Genres = genres.AsReadOnly();
        Categories = dataManager.GetExcelSheet<JournalCategory>()!
            .Where(x => x.RowId > 0)
            .Select(x => new Category(x, Genres.Where(y => y.CategoryId == x.RowId).ToList()))
            .ToList()
            .AsReadOnly();
        Sections = dataManager.GetExcelSheet<JournalSection>()!
            .Select(x => new Section(x, Categories.Where(y => y.SectionId == x.RowId).ToList()))
            .ToList();
    }

    public IReadOnlyList<Genre> Genres { get; }
    public IReadOnlyList<Category> Categories { get; }
    public List<Section> Sections { get; set; }

    internal sealed class Genre
    {
        public Genre(JournalGenre journalGenre, List<QuestInfo> quests)
        {
            Id = journalGenre.RowId;
            Name = journalGenre.Name.ToString();
            CategoryId = journalGenre.JournalCategory.Row;
            Quests = quests;
        }

        public Genre(uint id, string name, uint categoryId, List<QuestInfo> quests)
        {
            Id = id;
            Name = name;
            CategoryId = categoryId;
            Quests = quests;
        }

        public uint Id { get; }
        public string Name { get; }
        public uint CategoryId { get; }
        public List<QuestInfo> Quests { get; }
        public int QuestCount => Quests.Count;
    }

    internal sealed class Category(JournalCategory journalCategory, IReadOnlyList<Genre> genres)
    {
        public uint Id { get; } = journalCategory.RowId;
        public string Name { get; } = journalCategory.Name.ToString();
        public uint SectionId { get; } = journalCategory.JournalSection.Row;
        public IReadOnlyList<Genre> Genres { get; } = genres;
        public int QuestCount => Genres.Sum(x => x.QuestCount);
    }

    internal sealed class Section(JournalSection journalSection, IReadOnlyList<Category> categories)
    {
        public uint Id { get; } = journalSection.RowId;
        public string Name { get; } = journalSection.Name.ToString();
        public IReadOnlyList<Category> Categories { get; } = categories;
        public int QuestCount => Categories.Sum(x => x.QuestCount);
    }
}