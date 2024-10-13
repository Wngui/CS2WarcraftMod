using CounterStrikeSharp.API.Core;
using System;

namespace WarcraftPlugin.Menu;

public class MenuOption
{
    public Menu Parent { get; set; }
    public string OptionDisplay { get; set; }
    public Action<CCSPlayerController, MenuOption>? OnChoose { get; set; }
    public int Index { get; set; }
    public Action<CCSPlayerController, MenuOption>? OnSelect { get; set; }
}