using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Menu;

internal static class MenuManager
{
    internal static void OpenMainMenu(CCSPlayerController player, Menu menu, int selectedOptionIndex = 0)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].OpenMainMenu(menu, selectedOptionIndex);
    }

    internal static void CloseMenu(CCSPlayerController player)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].OpenMainMenu(null);
    }

    internal static void CloseSubMenu(CCSPlayerController player)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].CloseSubMenu();
    }

    internal static void CloseAllSubMenus(CCSPlayerController player)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].CloseAllSubMenus();
    }

    internal static void OpenSubMenu(CCSPlayerController player, Menu menu)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].OpenSubMenu(menu);
    }

    internal static Menu CreateMenu(string title = "", int resultsBeforePaging = 4)
    {
        Menu menu = new()
        {
            Title = title,
            ResultsBeforePaging = resultsBeforePaging,
        };
        return menu;
    }
}