using CounterStrikeSharp.API.Core;
using System;
using System.Collections.Generic;

namespace WarcraftPlugin.Menu;

public class Menu
{
    public string Title { get; set; } = "";
    public LinkedList<MenuOption> Options { get; set; } = new();
    public LinkedListNode<MenuOption> Prev { get; set; } = null;
    public LinkedListNode<MenuOption> Add(string display, string subDisplay, Action<CCSPlayerController, MenuOption> onChoice, Action<CCSPlayerController, MenuOption> onSelect = null)
    {
        if (Options == null)
            Options = new();
        MenuOption newOption = new MenuOption
        {
            OptionDisplay = display,
            SubOptionDisplay = subDisplay,
            OnChoose = onChoice,
            OnSelect = onSelect,
            Index = Options.Count,
            Parent = this
        };
        return Options.AddLast(newOption);
    }
}