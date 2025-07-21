using CounterStrikeSharp.API.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Menu.WarcraftMenu
{
    internal static class ClassMenu
    {
        internal static void Show(CCSPlayerController player, List<ClassInformation> classInformations)
        {
            var plugin = WarcraftPlugin.Instance;

            // Create a dictionary for fast lookups by InternalName with case-insensitive comparison
            var classInfoDict = classInformations?.ToDictionary(
                x => x.RaceName,
                x => x,
                StringComparer.OrdinalIgnoreCase);

            var warcraftClassInformations = new List<WarcraftClassInformation>();
            foreach (var warcraftClass in plugin.classManager.GetAllClasses())
            {
                // Try to get the class information from the dictionary
                classInfoDict.TryGetValue(warcraftClass.InternalName, out var classInformation);

                // Add to warcraftClassInformations with classInformation if found
                warcraftClassInformations.Add(new WarcraftClassInformation()
                {
                    DisplayName = warcraftClass?.LocalizedDisplayName,
                    InternalName = warcraftClass?.InternalName,
                    CurrentLevel = classInformation != null ? classInformation.CurrentLevel : 0,
                    CurrentXp = classInformation?.CurrentXp ?? 0,
                    DefaultColor = warcraftClass.DefaultColor,
                    TotalLevelRequired = WarcraftPlugin.Instance.Config.TotalLevelRequired
                    .FirstOrDefault(x => x.Key.Equals(warcraftClass.InternalName, StringComparison.OrdinalIgnoreCase)
                        || x.Key.Equals(warcraftClass.DisplayName, StringComparison.OrdinalIgnoreCase)
                        || x.Key.Equals(warcraftClass.LocalizedDisplayName, StringComparison.OrdinalIgnoreCase)).Value
                });
            }

            var totalLevels = warcraftClassInformations.Sum(x => x.CurrentLevel);

            var classMenu = MenuManager.CreateMenu(@$"<font color='lightgrey' class='{FontSizes.FontSizeM}'>{plugin.Localizer["menu.class"]}</font><br><font color='grey' class='{FontSizes.FontSizeS}'>{plugin.Localizer["menu.class.total.levels"]} (</font><font color='gold' class='{FontSizes.FontSizeS}'>{totalLevels}</font><font color='grey' class='{FontSizes.FontSizeS}'>)</font>", 5);

            foreach (var warClassInformation in warcraftClassInformations
                .OrderByDescending(x => x.CurrentLevel)
                .ThenByDescending(x => x.CurrentXp)
                .ThenBy(x => x.TotalLevelRequired)
                .ThenBy(x => x.DisplayName))
            {
                if (!WarcraftPlugin.Instance.classManager.GetAllClasses().Any(x => x.InternalName == warClassInformation.InternalName))
                {
                    continue;
                }

                var levelColor = TransitionToGold(warClassInformation.CurrentLevel / WarcraftPlugin.MaxLevel);

                var isCurrentClass = player.GetWarcraftPlayer().className == warClassInformation.InternalName;
                // Check if the class is locked based on total levels required
                var isLocked = warClassInformation.TotalLevelRequired >= totalLevels;
                var classDisplayColor = isLocked || isCurrentClass ? Color.Gray.Name : "white";

                var sb = new System.Text.StringBuilder();
                // Class colored bracket [
                sb.Append($"<font color='{warClassInformation.DefaultColor.AdjustBrightness(1.3f).ToHex()}' class='{FontSizes.FontSizeSm}'>(</font>");
                // Class colored name
                sb.Append($"<font color='{classDisplayColor}' class='{FontSizes.FontSizeSm}'>{warClassInformation.DisplayName}</font>");
                // Class colored bracket ]
                sb.Append($"<font color='{warClassInformation.DefaultColor.AdjustBrightness(1.3f).ToHex()}' class='{FontSizes.FontSizeSm}'>)</font>");
                // Level information
                if (isLocked)
                    sb.Append($"<font color='{Color.Gray.Name}' class='{FontSizes.FontSizeSm}'> - {plugin.Localizer["menu.class.locked", $"<font color='{Color.Gold.Name}' class='{FontSizes.FontSizeSm}'>{warClassInformation.TotalLevelRequired}</font>"]}</font>");
                else
                    sb.Append($"<font color='{levelColor.ToHex()}' class='{FontSizes.FontSizeSm}'> - {plugin.Localizer["menu.class.level"]} {warClassInformation.CurrentLevel}</font>");

                var displayString = sb.ToString();

                var classInternalName = warClassInformation.InternalName;

                classMenu.Add(displayString, null, (p, opt) =>
                {
                    if (!isCurrentClass && !isLocked)
                    {
                        p.PlayLocalSound("sounds/buttons/button9.vsnd");
                        MenuManager.CloseMenu(player);

                        if (player.IsValid)
                        {
                            if (!player.PawnIsAlive)
                            {
                                plugin.ChangeClass(player, classInternalName);
                            }
                            else
                            {
                                player.GetWarcraftPlayer().DesiredClass = classInternalName;
                                player.PrintToChat($" {plugin.Localizer["class.pending.change", warClassInformation.DisplayName]}");
                            }
                        }

                    }
                    else
                    {
                        p.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                    }
                });
            }

            MenuManager.OpenMainMenu(player, classMenu);
        }

        internal static Color TransitionToGold(float t)
        {
            // Ensure t is clamped between 0 and 1
            t = Math.Clamp(t, 0.1f, 1.0f);

            // Define the light grey and gold colors
            Color lightGrey = Color.FromArgb(240, 240, 240); // Light grey (211, 211, 211)
            Color gold = Color.FromArgb(255, 215, 0);        // Gold (255, 215, 0)

            // Linearly interpolate between the two colors
            int r = (int)(lightGrey.R + (gold.R - lightGrey.R) * t);
            int g = (int)(lightGrey.G + (gold.G - lightGrey.G) * t);
            int b = (int)(lightGrey.B + (gold.B - lightGrey.B) * t);

            // Return the new interpolated color
            return Color.FromArgb(r, g, b);
        }
    }

    internal class WarcraftClassInformation
    {
        internal string DisplayName { get; set; }
        internal string InternalName { get; set; }
        internal int CurrentLevel { get; set; }
        internal float CurrentXp { get; set; }
        internal Color DefaultColor { get; set; }
        public int TotalLevelRequired { get; set; }
    }

}
