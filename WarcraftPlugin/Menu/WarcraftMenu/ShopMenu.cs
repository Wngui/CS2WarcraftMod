using WarcraftPlugin.Items;
using CounterStrikeSharp.API;
using System.Linq;
using WarcraftPlugin.Models;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Menu.WarcraftMenu;

internal static class ShopMenu
{
    internal static void Show(WarcraftPlayer wcPlayer)
    {
        var menu = MenuManager.CreateMenu("<font color='lightgrey' class='fontSize-m'>Item Shop</font>");
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

        foreach (var item in items)
        {
            menu.Add($"<font color='white' class='{FontSizes.FontSizeSm}'>{item.LocalizedName} - ${item.Price}</font>",
                $"<font color='grey' class='{FontSizes.FontSizeS}'>{item.LocalizedDescription}</font>", (player, option) =>
            {
                if (!item.IsInstant)
                {
                    if (wcPlayer.Items.Count >= 2)
                    {
                        player.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                        player.PrintToChat(" You cannot carry more items.");
                        return;
                    }

                    if (wcPlayer.Items.Any(inv => inv.GetType() == item.GetType()))
                    {
                        player.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                        player.PrintToChat(" You already own this item.");
                        return;
                    }
                }

                try
                {
                    if (player.InGameMoneyServices.Account < item.Price)
                    {
                        player.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                        player.PrintToChat(" Not enough money!");
                        return;
                    }

                    // Print debug info of player's money before purchase
                    var moneyBefore = player.InGameMoneyServices.Account;
                    Server.PrintToConsole($"[Shop] {player.GetRealPlayerName()} money before purchase: {moneyBefore}");

                    // Directly modify the account balance so the deduction always applies
                    player.InGameMoneyServices.Account -= item.Price;

                    // Print debug info of player's money after purchase
                    var moneyAfter = player.InGameMoneyServices.Account;
                    Server.PrintToConsole($"[Shop] {player.GetRealPlayerName()} money after purchase: {moneyAfter}");

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
                    player.PlayLocalSound("sounds/buttons/button9.vsnd");
                    player.PrintToChat($" Bought {item.LocalizedName}");
                }
                else if (wcPlayer.AddItem(item))
                {
                    player.PlayLocalSound("sounds/buttons/button9.vsnd");
                    player.PrintToChat($" Bought {item.LocalizedName}");
                }
            });
        }

        MenuManager.OpenMainMenu(wcPlayer.Player, menu);
    }
}
