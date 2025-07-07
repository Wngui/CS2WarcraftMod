using CounterStrikeSharp.API;
using System.Linq;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Items;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Menu.WarcraftMenu;

internal static class ShopMenu
{
    internal static void Show(WarcraftPlayer wcPlayer)
    {
        var menu = MenuManager.CreateMenu($"<font color='lightgrey' class='{FontSizes.FontSizeM}'>" + ShopItem.Localizer["menu.shop"] + "</font>", 4);
        var plugin = WarcraftPlugin.Instance;

        var items = new ShopItem[]
        {
            new BootsOfSpeed(),
            new SockOfFeathers(),
            new RingOfRegeneration(),
            new MaskOfDeath(),
            new AmuletOfTheCat(),
            new DaggerOfVenom(),
            new MoneySiphonScepter(),
            new OrbOfFrost(),
            new TalismanOfEvasion(),
            new AmuletOfVitality(),
            new GlovesOfWrath(),
            new GlovesOfCloud(),
            new GlovesOfDazzle(),

            new TomeOfExperience(),
            new TomeOfGambling()
        };

        foreach (var item in items.OrderBy(x => x.Price))
        {
            menu.Add($"<font color='{item.Color.ToHex()}' class='{FontSizes.FontSizeSm}'>{item.LocalizedName}</font><font class='{FontSizes.FontSizeSm}'> - </font><font color='lightgreen' class='{FontSizes.FontSizeSm}'>${item.Price}</font>",
                $"<font color='#D3D3D3' class='{FontSizes.FontSizeS}'>{item.LocalizedDescription}</font>", (player, option) =>
            {
                if (!item.IsInstant)
                {
                    if (wcPlayer.Items.Count >= 2)
                    {
                        player.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                        player.PrintToChat($" {ShopItem.Localizer["menu.shop.carry_limit"]}");
                        return;
                    }

                    if (wcPlayer.Items.Any(inv => inv.GetType() == item.GetType()))
                    {
                        player.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                        player.PrintToChat($" {ShopItem.Localizer["menu.shop.already_owned"]}");
                        return;
                    }
                }

                try
                {
                    if (player.InGameMoneyServices.Account < item.Price)
                    {
                        player.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                        player.PrintToChat($" {ShopItem.Localizer["menu.shop.not_enough_money"]}");
                        return;
                    }

                    // Directly modify the account balance so the deduction always applies
                    player.InGameMoneyServices.Account -= item.Price;

                    // Notify the client that the player's money service has
                    // changed so the HUD reflects the new balance. The
                    // 'm_pInGameMoneyServices' netprop of the controller needs
                    // to be marked dirty for the UI to update correctly.
                    Utilities.SetStateChanged(player,
                        "CCSPlayerController", "m_pInGameMoneyServices");
                }
                catch
                {
                    // Fallback if money services not available
                    return;
                }

                if (item.IsInstant)
                {
                    item.Apply(player);
                }
                else if (wcPlayer.AddItem(item))
                {
                    item.Apply(player);
                }

                player.PlayLocalSound("sounds/buttons/button9.vsnd");
                player.PrintToChat($" {ShopItem.Localizer["menu.shop.bought", item.LocalizedName]}");
            });
        }

        MenuManager.OpenMainMenu(wcPlayer.Player, menu);
    }
}
