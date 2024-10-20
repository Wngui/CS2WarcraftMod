using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Menu.WarcraftMenu
{
    public static class ClassMenu
    {
        public static void Show(CCSPlayerController? player, List<DatabaseClassInformation> classInformations)
        {
            var plugin = WarcraftPlugin.Instance;

            // Create a dictionary for fast lookups by InternalName
            var classInfoDict = classInformations?.ToDictionary(x => x.RaceName, x => x);

            var warcraftClassInformations = new List<WarcraftClassInformation>();
            foreach (var warcraftClass in plugin.classManager.GetAllClasses())
            {
                // Try to get the class information from the dictionary
                classInfoDict.TryGetValue(warcraftClass.InternalName, out var classInformation);

                // Add to warcraftClassInformations with classInformation if found
                warcraftClassInformations.Add(new WarcraftClassInformation()
                {
                    DisplayName = warcraftClass?.DisplayName,
                    InternalName = warcraftClass?.InternalName,
                    CurrentLevel = classInformation != null ? classInformation.CurrentLevel : 1,
                    CurrentXp = classInformation?.CurrentXp ?? 0,
                    DefaultColor = warcraftClass.DefaultColor,
                });
            }

            var totalLevels = warcraftClassInformations.Sum(x => x.CurrentLevel);

            var classMenu = MenuManager.CreateMenu(@$"<font color='orange' class='{FontSizes.FontSizeM}'>Warcraft Class Menu</font><font color='orange' class='{FontSizes.FontSizeSm}'> ( </font><font color='gold' class='{FontSizes.FontSizeM}'>!class</font><font color='orange' class='{FontSizes.FontSizeSm}'> )</font><br><font color='grey' class='{FontSizes.FontSizeS}'>Total Levels (</font><font color='gold' class='{FontSizes.FontSizeS}'>{totalLevels}</font><font color='grey' class='{FontSizes.FontSizeS}'>)</font>", 5);

            foreach (var warClassInformation in warcraftClassInformations
                .OrderByDescending(x => x.CurrentLevel)
                .ThenByDescending(x => x.CurrentXp)
                .ThenBy(x => x.DisplayName))
            {
                if (WarcraftPlugin.Instance.Config.DeactivatedClasses.Contains(warClassInformation.InternalName, StringComparer.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                var levelColor = TransitionToGold(warClassInformation.CurrentLevel / WarcraftPlugin.MaxLevel);

                var isCurrentClass = player.GetWarcraftPlayer().className == warClassInformation.InternalName;

                var displayString = @$"<font color='{warClassInformation.DefaultColor.AdjustBrightness(1.3f).ToHex()}' class='{FontSizes.FontSizeS}'>(</font>
                <font color='{(isCurrentClass ? Color.Gray.Name : "white")}' class='{FontSizes.FontSizeSm}'>{warClassInformation.DisplayName}</font>
                <font color='{warClassInformation.DefaultColor.AdjustBrightness(1.3f).ToHex()}' class='{FontSizes.FontSizeS}'>)</font>
                <font color='{levelColor.ToHex()}' class='{FontSizes.FontSizeS}'>- Level {warClassInformation.CurrentLevel}</font>";

                var classInternalName = warClassInformation.InternalName;
                classMenu.Add(displayString, null, (p, opt) =>
                {
                    if (!isCurrentClass)
                    {
                        p.PlayLocalSound("sounds/buttons/button9.vsnd");
                        MenuManager.CloseMenu(player);
                        plugin.ChangeClass(player, classInternalName);
                    }
                    else {
                        p.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                    }
                });
            }

            MenuManager.OpenMainMenu(player, classMenu);
        }

        public static Color TransitionToGold(float t)
        {
            // Ensure t is clamped between 0 and 1
            t = Math.Clamp(t, 0.1f, 1.0f);

            // Define the light grey and gold colors
            Color lightGrey = Color.FromArgb(211, 211, 211); // Light grey (211, 211, 211)
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
        public string DisplayName { get; set; }
        public string InternalName { get; set; }
        public int CurrentLevel { get; set; }
        public float CurrentXp { get; set; }
        public Color DefaultColor { get; internal set; }
    }

}
