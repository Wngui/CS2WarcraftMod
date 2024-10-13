using System.Collections.Generic;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using Microsoft.Extensions.Localization;

namespace WarcraftPlugin.Menu;

public class MenuPlayer
{
    public CCSPlayerController player { get; set; }
    public Menu MainMenu = null;
    public LinkedListNode<MenuOption>? CurrentChoice = null;
    public LinkedListNode<MenuOption>? MenuStart = null;
    public string CenterHtml = "";
    public int VisibleOptions = 5;
    public static IStringLocalizer Localizer = null;
    public PlayerButtons Buttons { get; set; }

    public void OpenMainMenu(Menu menu)
    {
        if (player.PlayerPawn.Value != null && player.PlayerPawn.Value.IsValid)
        {
            player.PlayerPawn.Value!.MoveType = MoveType_t.MOVETYPE_NONE;
            Schema.SetSchemaValue(player.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 0);
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
        }

        if (menu == null)
        {
            if (player.PlayerPawn.Value != null && player.PlayerPawn.Value.IsValid)
            {
                player.PlayerPawn.Value!.MoveType = MoveType_t.MOVETYPE_WALK;
                Schema.SetSchemaValue(player.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 2);
                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
            }
            MainMenu = null;
            CurrentChoice = null;
            CenterHtml = "";
            return;
        }
        MainMenu = menu;
        VisibleOptions = menu.Title != "" ? 4 : 5;
        CurrentChoice = MainMenu.Options?.First;
        MenuStart = CurrentChoice;

        CurrentChoice?.Value.OnSelect?.Invoke(player, CurrentChoice.Value);

        UpdateCenterHtml();
    }

    public void OpenSubMenu(Menu menu)
    {
        if (menu == null)
        {
            CurrentChoice = MainMenu?.Options?.First;
            MenuStart = CurrentChoice;
            UpdateCenterHtml();
            return;
        }

        VisibleOptions = menu.Title != "" ? 4 : 5;
        CurrentChoice = menu.Options?.First;
        MenuStart = CurrentChoice;
        UpdateCenterHtml();
    }
    public void GoBackToPrev(LinkedListNode<MenuOption>? menu)
    {
        if (menu == null)
        {
            CurrentChoice = MainMenu?.Options?.First;
            MenuStart = CurrentChoice;
            UpdateCenterHtml();
            return;
        }

        VisibleOptions = menu.Value.Parent?.Title != "" ? 4 : 5;
        CurrentChoice = menu;
        if (CurrentChoice.Value.Index >= 5)
        {
            MenuStart = CurrentChoice;
            for (int i = 0; i < 4; i++)
            {
                MenuStart = MenuStart?.Previous;
            }
        }
        else
            MenuStart = CurrentChoice.List?.First;
        UpdateCenterHtml();
    }

    public void CloseSubMenu()
    {
        if (CurrentChoice?.Value.Parent?.Prev == null)
        {
            if (player.PlayerPawn.Value != null && player.PlayerPawn.Value.IsValid)
            {
                player.PlayerPawn.Value!.MoveType = MoveType_t.MOVETYPE_WALK;
                Schema.SetSchemaValue(player.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 2);
                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
            }

            return;
        }
        GoBackToPrev(CurrentChoice?.Value.Parent.Prev);
    }

    public void CloseAllSubMenus()
    {
        OpenSubMenu(null);
    }

    public void Choose()
    {
        CurrentChoice?.Value.OnChoose?.Invoke(player, CurrentChoice.Value);
    }

    public void ScrollDown()
    {
        if (CurrentChoice == null || MainMenu == null)
            return;
        CurrentChoice = CurrentChoice.Next ?? CurrentChoice.List?.First;
        MenuStart = CurrentChoice!.Value.Index >= VisibleOptions ? MenuStart!.Next : CurrentChoice.List?.First;

        CurrentChoice?.Value.OnSelect?.Invoke(player, CurrentChoice.Value);

        UpdateCenterHtml();
    }

    public void ScrollUp()
    {
        if (CurrentChoice == null || MainMenu == null)
            return;
        CurrentChoice = CurrentChoice.Previous ?? CurrentChoice.List?.Last;
        if (CurrentChoice == CurrentChoice?.List?.Last && CurrentChoice?.Value.Index >= VisibleOptions)
        {
            MenuStart = CurrentChoice;
            for (int i = 0; i < VisibleOptions - 1; i++)
                MenuStart = MenuStart?.Previous;
        }
        else
            MenuStart = CurrentChoice!.Value.Index >= VisibleOptions ? MenuStart!.Previous : CurrentChoice.List?.First;

        CurrentChoice?.Value.OnSelect?.Invoke(player, CurrentChoice.Value);

        UpdateCenterHtml();
    }

    private void UpdateCenterHtml()
    {
        if (CurrentChoice == null || MainMenu == null)
            return;

        StringBuilder builder = new StringBuilder();
        int i = 0;
        LinkedListNode<MenuOption>? option = MenuStart!;
        builder.AppendLine($"{option.Value.Parent?.Title}<br>");

        while (i < VisibleOptions && option != null)
        {
            if (option == CurrentChoice)
            {
                builder.AppendLine($"{Localizer?["menu.selection.left"]} {option.Value.OptionDisplay} {Localizer?["menu.selection.right"]}<br>");
                if (option.Value.SubOptionDisplay != null) builder.AppendLine($"{option.Value.SubOptionDisplay}<br>");
            }
            else
            {
                builder.AppendLine($"{option.Value.OptionDisplay} <br>");
            }
            option = option.Next;
            i++;
        }

        if (option != null)
        { // more options
            builder.AppendLine(
                $"{Localizer?["menu.more.options.below"]}");
        }

        builder.AppendLine("<br>");
        builder.AppendLine($"{Localizer?["menu.bottom.text"]}");
        builder.AppendLine("<br>");
        CenterHtml = builder.ToString();
    }
}