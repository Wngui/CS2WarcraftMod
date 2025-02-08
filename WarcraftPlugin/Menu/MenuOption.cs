using CounterStrikeSharp.API.Core;
using System;

namespace WarcraftPlugin.Menu;

internal class MenuOption
{
    internal Menu Parent { get; set; }
    internal string OptionDisplay { get; set; }
    internal string SubOptionDisplay { get; set; }
    internal Action<CCSPlayerController, MenuOption>? OnChoose { get; set; }
    internal int Index { get; set; }
    internal Action<CCSPlayerController, MenuOption>? OnSelect { get; set; }
}