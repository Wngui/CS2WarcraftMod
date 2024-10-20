using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Menu;

public static class MenuManager
{
    public static void OpenMainMenu(CCSPlayerController player, Menu menu, int selectedOptionIndex = 0)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].OpenMainMenu(menu, selectedOptionIndex);
    }

    public static void CloseMenu(CCSPlayerController player)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].OpenMainMenu(null);
    }

    public static void CloseSubMenu(CCSPlayerController player)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].CloseSubMenu();
    }

    public static void CloseAllSubMenus(CCSPlayerController player)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].CloseAllSubMenus();
    }

    public static void OpenSubMenu(CCSPlayerController player, Menu menu)
    {
        if (player == null)
            return;
        MenuAPI.Players[player.Slot].OpenSubMenu(menu);
    }

    public static Menu CreateMenu(string title = "", int resultsBeforePaging = 4)
    {
        Menu menu = new()
        {
            Title = title,
            ResultsBeforePaging = resultsBeforePaging,
        };
        return menu;
    }
}