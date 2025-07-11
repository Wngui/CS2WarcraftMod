﻿using System.Drawing;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Menu.WarcraftMenu
{
    internal static class SkillsMenu
    {
        internal static void Show(WarcraftPlayer wcPlayer, int selectedOptionIndex = 0)
        {
            var plugin = WarcraftPlugin.Instance;

            var warcraftClass = wcPlayer.GetClass();

            var skillsMenu = MenuManager.CreateMenu(@$"<font color='{warcraftClass.DefaultColor.AdjustBrightness(1.3f).ToHex()}' class='{FontSizes.FontSizeM}'>{warcraftClass.LocalizedDisplayName}</font><font color='gold' class='{FontSizes.FontSizeSm}'> - {plugin.Localizer["menu.skills.level"]} {wcPlayer.GetLevel()}</font><br>
                <font color='#90EE90' class='{FontSizes.FontSizeS}'>{plugin.Localizer["menu.skills.available", XpSystem.GetFreeSkillPoints(wcPlayer)]}</font>");

            for (int i = 0; i < warcraftClass.Abilities.Count; i++)
            {
                var ability = warcraftClass.GetAbility(i);
                var abilityLevel = wcPlayer.GetAbilityLevel(i);
                var maxAbilityLevel = WarcraftPlayer.GetMaxAbilityLevel(i);

                var isUltimate = i == WarcraftPlayer.UltimateAbilityIndex;
                var isDisabled = false;

                if (abilityLevel == maxAbilityLevel || XpSystem.GetFreeSkillPoints(wcPlayer) == 0)
                {
                    isDisabled = true;
                }

                var color = isDisabled ? Color.Gray : Color.White;

                var abilityLevelColor = abilityLevel > 0 ? "#90EE90" : "white";

                var abilityProgressString = (abilityLevel == maxAbilityLevel)
                    ? $"<font color='darkgrey'>({abilityLevel}/{maxAbilityLevel})</font>"
                    : $"<font color='darkgrey'>(</font>" +
                      $"<font color='{abilityLevelColor}'>{abilityLevel}</font>" +
                      $"<font color='darkgrey'>/{maxAbilityLevel})</font>";

                var displayString = $"<font color='{color.Name}' class='{FontSizes.FontSizeSm}'>{ability.DisplayName} {abilityProgressString}</font>";

                if (isUltimate && abilityLevel != maxAbilityLevel) //Ultimate ability
                {
                    if (wcPlayer.IsMaxLevel)
                    {
                        color = Color.MediumPurple;
                        displayString = $"<font color='{color.Name}' class='{FontSizes.FontSizeSm}'>{ability.DisplayName} {abilityProgressString}</font>";
                    }
                    else
                    {
                        isDisabled = true;
                        color = Color.Gray;
                        displayString = $"<font color='{color.Name}' class='{FontSizes.FontSizeSm}'>{ability.DisplayName} ({plugin.Localizer["menu.skills.ultimate.level", WarcraftPlugin.MaxLevel]})</font>";
                    }
                }

                var subDisplayString = $"<font color='#D3D3D3' class='{FontSizes.FontSizeS}'>{ability.Description}</font>";

                var abilityIndex = i;
                skillsMenu.Add(displayString, subDisplayString, (p, opt) =>
                {
                    if (!isDisabled)
                    {
                        wcPlayer.GrantAbilityLevel(abilityIndex);
                    }
                    else
                    {
                        p.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                    }

                    Show(wcPlayer, opt.Index);
                });
            }

            MenuManager.OpenMainMenu(wcPlayer.Player, skillsMenu, selectedOptionIndex);
        }
    }
}
