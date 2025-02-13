using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using Microsoft.Extensions.Localization;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Menu;

internal class MenuPlayer
{
    internal CCSPlayerController player { get; set; }
    internal Menu MainMenu = null;
    internal LinkedListNode<MenuOption> CurrentChoice = null;
    internal LinkedListNode<MenuOption> MenuStart = null;
    internal string CenterHtml = "";
    internal int VisibleOptions = 5;
    internal static IStringLocalizer Localizer = null;
    internal PlayerButtons Buttons { get; set; }

    internal void OpenMainMenu(Menu menu, int selectedOptionIndex = 0)
    {
        player.DisableMovement();

        if (menu == null)
        {
            player.EnableMovement();
            MainMenu = null;
            CurrentChoice = null;
            CenterHtml = "";
            return;
        }
        MainMenu = menu;
        VisibleOptions = menu.ResultsBeforePaging;
        MenuStart = MainMenu.Options?.First;
        CurrentChoice = MenuStart;

        //Set the selected option based on index
        for (int i = 0; i < selectedOptionIndex; i++)
        {
            CurrentChoice = CurrentChoice.Next;
        }

        CurrentChoice?.Value.OnSelect?.Invoke(player, CurrentChoice.Value);

        UpdateCenterHtml();
    }

    internal void OpenSubMenu(Menu menu)
    {
        if (menu == null)
        {
            CurrentChoice = MainMenu?.Options?.First;
            MenuStart = CurrentChoice;
            UpdateCenterHtml();
            return;
        }

        VisibleOptions = menu.ResultsBeforePaging;
        CurrentChoice = menu.Options?.First;
        MenuStart = CurrentChoice;
        UpdateCenterHtml();
    }
    internal void GoBackToPrev(LinkedListNode<MenuOption> menu)
    {
        if (menu == null)
        {
            CurrentChoice = MainMenu?.Options?.First;
            MenuStart = CurrentChoice;
            UpdateCenterHtml();
            return;
        }

        VisibleOptions = menu.Value.Parent?.ResultsBeforePaging ?? 4;
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

    internal void CloseSubMenu()
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

    internal void CloseAllSubMenus()
    {
        OpenSubMenu(null);
    }

    internal void Choose()
    {
        CurrentChoice?.Value.OnChoose?.Invoke(player, CurrentChoice.Value);
    }

    internal void ScrollDown()
    {
        if (CurrentChoice == null || MainMenu == null)
            return;
        CurrentChoice = CurrentChoice.Next ?? CurrentChoice.List?.First;
        MenuStart = CurrentChoice!.Value.Index >= VisibleOptions ? MenuStart!.Next : CurrentChoice.List?.First;

        CurrentChoice?.Value.OnSelect?.Invoke(player, CurrentChoice.Value);

        UpdateCenterHtml();
    }

    internal void ScrollUp()
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
        LinkedListNode<MenuOption> option = MenuStart!;
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
                builder.AppendLine($"{option.Value.OptionDisplay}<br>");
            }
            option = option.Next;
            i++;
        }

        if (option != null)
        {
            builder.AppendLine($"{Localizer?["menu.more.options.below"]}");
        }
        if (option == null && MenuStart.List.Count > VisibleOptions)
        {
            builder.AppendLine($"<center><img src='https://dummyimage.com/1x16/000/fff'></center><br>");
        }

        if (CurrentChoice?.Value?.SubOptionDisplay != null)
        {
            var subOptionTextSpace = CalculateTextSpace(CurrentChoice?.Value?.SubOptionDisplay);
            if (subOptionTextSpace < 56)
            {
                builder.AppendLine($"<font class='{FontSizes.FontSizeM}'>ㅤ</font><br>");
            }
            else
            {
                builder.AppendLine($"<font class='{FontSizes.FontSizeXs}'>ㅤ</font><br>");
            }
        }

        var selectKey = player.IsAlive() ? "Space" : "E";
        builder.AppendLine($"<center><font color='red' class='fontSize-sm'>Navigate:</font><font color='orange' class='fontSize-s'> W↑ S↓</font><font color='white' class='fontSize-sm'> | </font><font color='red' class='fontSize-sm'>Select: </font><font color='orange' class='fontSize-sm'>{selectKey}</font><font color='white' class='fontSize-sm'> | </font><font color='red' class='fontSize-sm'>Exit: </font><font color='orange' class='fontSize-sm'>Tab</font></center>");
        builder.AppendLine("<br>");
        CenterHtml = builder.ToString();
    }

    private static int CalculateTextSpace(string subOptionDisplay)
    {
        // Use a regular expression to remove all HTML-like tags
        string pattern = @"<[^>]+>";
        string cleanedString = Regex.Replace(subOptionDisplay, pattern, string.Empty);

        // Return the length of the cleaned string
        return cleanedString.Length;
    }
}