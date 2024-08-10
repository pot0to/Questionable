﻿using Dalamud.Game.Text.SeStringHandling;

namespace Questionable.Controller.Steps;

public interface IToastAware
{
    void OnErrorToast(SeString message);
}