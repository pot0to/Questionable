﻿using System.Collections.Generic;
using Questionable.Model.Common.Converter;

namespace Questionable.Model.Questing.Converter;

public sealed class ActionConverter() : EnumConverter<EAction>(Values)
{
    private static readonly Dictionary<EAction, string> Values = new()
    {
        { EAction.Cure, "Cure" },
        { EAction.Esuna, "Esuna" },
        { EAction.Physick, "Physick" },
        { EAction.Buffet, "Buffet" },
        { EAction.Fumigate, "Fumigate" },
        { EAction.SiphonSnout, "Siphon Snout" },
        { EAction.RedGulal, "Red Gulal" },
        { EAction.YellowGulal, "Yellow Gulal" },
        { EAction.BlueGulal, "Blue Gulal" },
    };
}
