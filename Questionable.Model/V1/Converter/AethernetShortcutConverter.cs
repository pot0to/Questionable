﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Questionable.Model.V1.Converter;

public sealed class AethernetShortcutConverter : JsonConverter<AethernetShortcut>
{
    private static readonly Dictionary<EAetheryteLocation, string> EnumToString = new()
    {
        { EAetheryteLocation.Gridania, "[Gridania] Aetheryte Plaza" },
        { EAetheryteLocation.GridaniaArcher, "[Gridania] Archers' Guild" },
        { EAetheryteLocation.GridaniaLeatherworker, "[Gridania] Leatherworkers' Guild & Shaded Bower" },
        { EAetheryteLocation.GridaniaLancer, "[Gridania] Lancers' Guild" },
        { EAetheryteLocation.GridaniaConjurer, "[Gridania] Conjurers' Guild" },
        { EAetheryteLocation.GridaniaBotanist, "[Gridania] Botanists' Guild" },
        { EAetheryteLocation.GridaniaAmphitheatre, "[Gridania] Mih Khetto's Amphitheatre" },
        { EAetheryteLocation.GridaniaBlueBadgerGate, "[Gridania] Blue Badger Gate (Central Shroud)" },
        { EAetheryteLocation.GridaniaYellowSerpentGate, "[Gridania] Yellow Serpent Gate (North Shroud)" },
        { EAetheryteLocation.GridaniaWhiteWolfGate, "[Gridania] White Wolf Gate (Central Shroud)" },
        { EAetheryteLocation.GridaniaAirship, "[Gridania] Airship Landing" },

        { EAetheryteLocation.Uldah, "[Ul'dah] Aetheryte Plaza" },
        { EAetheryteLocation.UldahAdventurers, "[Ul'dah] Adventurers' Guild" },
        { EAetheryteLocation.UldahThaumaturge, "[Ul'dah] Thaumaturges' Guild" },
        { EAetheryteLocation.UldahGladiator, "[Ul'dah] Gladiators' Guild" },
        { EAetheryteLocation.UldahMiner, "[Ul'dah] Miners' Guild" },
        { EAetheryteLocation.UldahWeaver, "[Ul'dah] Weavers' Guild" },
        { EAetheryteLocation.UldahGoldsmith, "[Ul'dah] Goldsmiths' Guild" },
        { EAetheryteLocation.UldahSapphireAvenue, "[Ul'dah] Sapphire Avenue Exchange" },
        { EAetheryteLocation.UldahAlchemist, "[Ul'dah] Alchemists' Guild" },
        { EAetheryteLocation.UldahChamberOfRule, "[Ul'dah] The Chamber of Rule" },
        { EAetheryteLocation.UldahGateOfTheSultana, "[Ul'dah] Gate of the Sultana (Western Thanalan)" },
        { EAetheryteLocation.UldahGateOfNald, "[Ul'dah] Gate of Nald (Central Thanalan)" },
        { EAetheryteLocation.UldahGateOfThal, "[Ul'dah] Gate of Thal (Central Thanalan)" },
        { EAetheryteLocation.UldahAirship, "[Ul'dah] Airship Landing" },

        { EAetheryteLocation.Limsa, "[Limsa Lominsa] Aetheryte Plaza" },
        { EAetheryteLocation.LimsaArcanist, "[Limsa Lominsa] Arcanists' Guild" },
        { EAetheryteLocation.LimsaFisher, "[Limsa Lominsa] Fishermens' Guild" },
        { EAetheryteLocation.LimsaHawkersAlley, "[Limsa Lominsa] Hawkers' Alley" },
        { EAetheryteLocation.LimsaAftcastle, "[Limsa Lominsa] The Aftcastle" },
        { EAetheryteLocation.LimsaCulinarian, "[Limsa Lominsa] Culinarians' Guild" },
        { EAetheryteLocation.LimsaMarauder, "[Limsa Lominsa] Marauders' Guild" },
        { EAetheryteLocation.LimsaZephyrGate, "[Limsa Lominsa] Zephyr Gate (Middle La Noscea)" },
        { EAetheryteLocation.LimsaTempestGate, "[Limsa Lominsa] Tempest Gate (Lower La Noscea)" },
        { EAetheryteLocation.LimsaAirship, "[Limsa Lominsa] Airship Landing" },

        { EAetheryteLocation.Ishgard, "[Ishgard] Aetheryte Plaza" },
        { EAetheryteLocation.IshgardForgottenKnight, "[Ishgard] The Forgotten Knight" },
        { EAetheryteLocation.IshgardSkysteelManufactory, "[Ishgard] Skysteel Manufactory" },
        { EAetheryteLocation.IshgardBrume, "[Ishgard] The Brume" },
        { EAetheryteLocation.IshgardAthenaeumAstrologicum, "[Ishgard] Athenaeum Astrologicum" },
        { EAetheryteLocation.IshgardJeweledCrozier, "[Ishgard] The Jeweled Crozier" },
        { EAetheryteLocation.IshgardSaintReymanaudsCathedral, "[Ishgard] Saint Reymanaud's Cathedral" },
        { EAetheryteLocation.IshgardTribunal, "[Ishgard] The Tribunal" },
        { EAetheryteLocation.IshgardLastVigil, "[Ishgard] The Last Vigil" },
        { EAetheryteLocation.IshgardGatesOfJudgement, "[Ishgard] The Gates of Judgement (Coerthas Central Highlands)" },

        { EAetheryteLocation.Idyllshire, "[Idyllshire] Aetheryte Plaza" },
        { EAetheryteLocation.IdyllshireWest, "[Idyllshire] West Idyllshire" },
        { EAetheryteLocation.IdyllshirePrologueGate, "[Idyllshire] Prologue Gate" },
        { EAetheryteLocation.IdyllshireEpilogueGate, "[Idyllshire] Epilogue Gate" },

        { EAetheryteLocation.RhalgrsReach, "[Rhalgr's Reach] Aetheryte Plaza" },
        { EAetheryteLocation.RhalgrsReachWest, "[Rhalgr's Reach] Western Rhalgr's Reach" },
        { EAetheryteLocation.RhalgrsReachNorthEast, "[Rhalgr's Reach] Northeastern Rhalgr's Reach" },
        { EAetheryteLocation.RhalgrsReachFringesGate, "[Rhalgr's Reach] Fringes Gate" },
        { EAetheryteLocation.RhalgrsReachPeaksGate, "[Rhalgr's Reach] Peaks Gate" },

        { EAetheryteLocation.Kugane, "[Kugane] Aetheryte Plaza" },
        { EAetheryteLocation.KuganeShiokazeHostelry, "[Kugane] Shiokaze Hostelry" },
        { EAetheryteLocation.KuganePier1, "[Kugane] Pier #1" },
        { EAetheryteLocation.KuganeThavnairianConsulate, "[Kugane] Thavnairian Consulate" },
        { EAetheryteLocation.KuganeMarkets, "[Kugane] Kogane Dori Markets" },
        { EAetheryteLocation.KuganeBokairoInn, "[Kugane] Bokairo Inn" },
        { EAetheryteLocation.KuganeRubyBazaar, "[Kugane] The Ruby Bazaar" },
        { EAetheryteLocation.KuganeSekiseigumiBarracks, "[Kugane] Sekiseigumi Barracks" },
        { EAetheryteLocation.KuganeRakuzaDistrict, "[Kugane] Rakuza District" },
        { EAetheryteLocation.KuganeRubyPrice, "[Kugane] The Ruby Price" },
        { EAetheryteLocation.KuganeAirship, "[Kugane] Airship Landing" },

        { EAetheryteLocation.Crystarium, "[Crystarium] Aetheryte Plaza" },
        { EAetheryteLocation.CrystariumMarkets, "[Crystarium] Musica Universalis Markets" },
        { EAetheryteLocation.CrystariumThemenosRookery, "[Crystarium] Themenos Rookery" },
        { EAetheryteLocation.CrystariumDossalGate, "[Crystarium] The Dossal Gate" },
        { EAetheryteLocation.CrystariumPendants, "[Crystarium] The Pendants" },
        { EAetheryteLocation.CrystariumAmaroLaunch, "[Crystarium] The Amaro Launch" },
        { EAetheryteLocation.CrystariumCrystallineMean, "[Crystarium] The Crystalline Mean" },
        { EAetheryteLocation.CrystariumCabinetOfCuriosity, "[Crystarium] The Cabinet of Curiosity" },
        { EAetheryteLocation.CrystariumTessellation, "[Crystarium] Tessellation (Lakeland)" },

        { EAetheryteLocation.Eulmore, "[Eulmore] Aetheryte Plaza" },
        { EAetheryteLocation.EulmoreSoutheastDerelict, "[Eulmore] Southeast Derelicts" },
        { EAetheryteLocation.EulmoreNightsoilPots, "[Eulmore] Nightsoil Pots" },
        { EAetheryteLocation.EulmoreGloryGate, "[Eulmore] The Glory Gate" },
        { EAetheryteLocation.EulmoreMainstay, "[Eulmore] The Mainstay" },
        { EAetheryteLocation.EulmorePathToGlory, "[Eulmore] The Path to Glory (Kholusia)" },

        { EAetheryteLocation.OldSharlayan, "[Old Sharlayan] Aetheryte Plaza" },
        { EAetheryteLocation.OldSharlayanStudium, "[Old Sharlayan] The Studium" },
        { EAetheryteLocation.OldSharlayanBaldesionAnnex, "[Old Sharlayan] The Baldesion Annex" },
        { EAetheryteLocation.OldSharlayanRostra, "[Old Sharlayan] The Rostra" },
        { EAetheryteLocation.OldSharlayanLeveilleurEstate, "[Old Sharlayan] The Leveilleur Estate" },
        { EAetheryteLocation.OldSharlayanJourneysEnd, "[Old Sharlayan] Journey's End" },
        { EAetheryteLocation.OldSharlayanScholarsHarbor, "[Old Sharlayan] Scholar's Harbor" },
        { EAetheryteLocation.OldSharlayanHallOfArtifice, "[Old Sharlayan] The Hall of Artifice (Labyrinthos)" },

        { EAetheryteLocation.RadzAtHan, "[Radz-at-Han] Aetheryte Plaza" },
        { EAetheryteLocation.RadzAtHanMeghaduta, "[Radz-at-Han] Meghaduta" },
        { EAetheryteLocation.RadzAtHanRuveydahFibers, "[Radz-at-Han] Ruveydah Fibers" },
        { EAetheryteLocation.RadzAtHanAirship, "[Radz-at-Han] Airship Landing" },
        { EAetheryteLocation.RadzAtHanAlzadaalsPeace, "[Radz-at-Han] Alzadaal's Peace" },
        { EAetheryteLocation.RadzAtHanHallOfTheRadiantHost, "[Radz-at-Han] Hall of the Radiant Host" },
        { EAetheryteLocation.RadzAtHanMehrydesMeyhane, "[Radz-at-Han] Mehryde's Meyhane" },
        { EAetheryteLocation.RadzAtHanKama, "[Radz-at-Han] Kama" },
        { EAetheryteLocation.RadzAtHanHighCrucible, "[Radz-at-Han] The High Crucible of Al-Kimiya" },
        { EAetheryteLocation.RadzAtHanGateOfFirstSight, "[Radz-at-Han] The Gate of First Sight (Thavnair)" },

        { EAetheryteLocation.Tuliyollal, "[Tuliyollal] Aetheryte Plaza" },
        { EAetheryteLocation.TuliyollalDirigibleLanding, "[Tuliyollal] Dirigible Landing" },
        { EAetheryteLocation.TuliyollalTheResplendentQuarter, "[Tuliyollal] The Resplendent Quarter" },
        { EAetheryteLocation.TuliyollalTheForardCabins, "[Tuliyollal] The For'ard Cabins" },
        { EAetheryteLocation.TuliyollalBaysideBevyMarketplace, "[Tuliyollal] Bayside Bevy Marketplace" },
        { EAetheryteLocation.TuliyollalVollokShoonsa, "[Tuliyollal] Vollok Shoonsa" },
        { EAetheryteLocation.TuliyollalWachumeqimeqi, "[Tuliyollal] Wachumeqimeqi" },
        { EAetheryteLocation.TuliyollalBrightploomPost, "[Tuliyollal] Brightploom Post" },
        { EAetheryteLocation.TuliyollalArchOfTheDawnUrqopacha, "[Tuliyollal] Arch of the Dawn (Urqopacha)" },
        { EAetheryteLocation.TuliyollalArchOfTheDawnKozamauka, "[Tuliyollal] Arch of the Dawn (Kozama'uka)" },
        { EAetheryteLocation.TuliyollalIhuykatumu, "[Tuliyollal] Ihuykatumu (Kozama'uka)" },
        { EAetheryteLocation.TuliyollalDirigibleLandingYakTel, "[Tuliyollal] Dirigible Landing (Yak T'el)" },
        { EAetheryteLocation.TuliyollalXakTuralSkygate, "[Tuliyollal] Xak Tural Skygate (Shaaloani)" },

        { EAetheryteLocation.SolutionNine, "[Solution Nine] Aetheryte Plaza" },
        { EAetheryteLocation.SolutionNineInformationCenter, "[Solution Nine] Information Center" },
        { EAetheryteLocation.SolutionNineTrueVue, "[Solution Nine] True Vue" },
        { EAetheryteLocation.SolutionNineNeonStein, "[Solution Nine] Neon Stein" },
        { EAetheryteLocation.SolutionNineTheArcadion, "[Solution Nine] The Arcadion" },
        { EAetheryteLocation.SolutionNineResolution, "[Solution Nine] Resolution" },
        { EAetheryteLocation.SolutionNineNexusArcade, "[Solution Nine] Nexus Arcade" },
        { EAetheryteLocation.SolutionNineResidentialSector, "[Solution Nine] Residential Sector" },
        { EAetheryteLocation.SolutionNineScanningPortNine, "[Solution Nine] Scanning Port Nine (Heritage Found)" },
    };

    private static readonly Dictionary<string, EAetheryteLocation> StringToEnum =
        EnumToString.ToDictionary(x => x.Value, x => x.Key);

    public override AethernetShortcut Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        string from = reader.GetString() ?? throw new JsonException();

        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            throw new JsonException();

        string to = reader.GetString() ?? throw new JsonException();

        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException();

        return new AethernetShortcut
        {
            From = StringToEnum.TryGetValue(from, out var fromEnum) ? fromEnum : throw new JsonException(),
            To = StringToEnum.TryGetValue(to, out var toEnum) ? toEnum : throw new JsonException()
        };
    }

    public override void Write(Utf8JsonWriter writer, AethernetShortcut value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(EnumToString[value.From]);
        writer.WriteStringValue(EnumToString[value.To]);
        writer.WriteEndArray();
    }
}
