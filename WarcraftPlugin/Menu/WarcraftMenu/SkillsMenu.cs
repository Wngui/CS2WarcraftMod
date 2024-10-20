﻿using System.Drawing;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Menu.WarcraftMenu
{
    public static class SkillsMenu
    {
        public static void Show(WarcraftPlayer wcPlayer, int selectedOptionIndex = 0)
        {
            var warcraftClass = wcPlayer.GetClass();

            var skillsMenu = MenuManager.CreateMenu(@$"<font color='{warcraftClass.DefaultColor.AdjustBrightness(1.3f).ToHex()}' class='{FontSizes.FontSizeM}'>{warcraftClass.DisplayName}</font><font color='gold' class='{FontSizes.FontSizeSm}'> - Level {wcPlayer.GetLevel()}</font><br>
                <font color='#90EE90' class='{FontSizes.FontSizeS}'>Level up skills ({XpSystem.GetFreeSkillPoints(wcPlayer)} available)</font>");

            for (int i = 0; i < 4; i++)
            {
                var ability = warcraftClass.GetAbility(i);
                var abilityLevel = wcPlayer.GetAbilityLevel(i);
                var maxAbilityLevel = WarcraftPlayer.GetMaxAbilityLevel(i);

                var color = (abilityLevel == maxAbilityLevel || XpSystem.GetFreeSkillPoints(wcPlayer) == 0)
                            ? Color.Gray
                            : Color.White;

                var abilityLevelColor = abilityLevel > 0 ? "#90EE90" : "white";

                var abilityProgressString = (abilityLevel == maxAbilityLevel)
                    ? $"<font color='darkgrey'>({abilityLevel}/{maxAbilityLevel})</font>"
                    : $"<font color='darkgrey'>(</font>" +
                      $"<font color='{abilityLevelColor}'>{abilityLevel}</font>" +
                      $"<font color='darkgrey'>/{maxAbilityLevel})</font>";

                var displayString = $"<font color='{color.Name}' class='{FontSizes.FontSizeSm}'>{ability.DisplayName} {abilityProgressString}</font>";


                if (i == 3 && abilityLevel != maxAbilityLevel) //Ultimate ability
                {
                    if (wcPlayer.IsMaxLevel)
                    {
                        color = Color.MediumPurple;
                        displayString = $"<font color='{color.Name}' class='{FontSizes.FontSizeSm}'>{ability.DisplayName} {abilityProgressString}</font>";
                    }
                    else
                    {
                        color = Color.Gray;
                        displayString = $"<font color='{color.Name}' class='{FontSizes.FontSizeSm}'>{ability.DisplayName} (level 16)</font>";
                    }
                }

                var subDisplayString = $"<font color='#D3D3D3' class='{FontSizes.FontSizeS}'>{ability.GetDescription(i)}</font>";

                var abilityIndex = i;
                skillsMenu.Add(displayString, subDisplayString, (p, opt) =>
                {
                    if (XpSystem.GetFreeSkillPoints(wcPlayer) > 0 && abilityLevel < WarcraftPlayer.GetMaxAbilityLevel(i))
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