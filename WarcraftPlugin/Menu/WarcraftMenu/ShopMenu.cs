using WarcraftPlugin.Items;
using WarcraftPlugin.Menu;
using CounterStrikeSharp.API.Core;
using System.Linq;
using System.Drawing;
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
            new SockOfFeathers()
        };

        foreach (var item in items)
        {
            menu.Add($"<font color='white' class='{FontSizes.FontSizeSm}'>{item.Name} - ${item.Price}</font>",
                $"<font color='grey' class='{FontSizes.FontSizeS}'>{item.Description}</font>", (player, option) =>
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

                try
                {
                    if (player.InGameMoneyServices.Account < item.Price)
                    {
                        player.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
                        player.PrintToChat(" Not enough money!");
                        return;
                    }

                    player.InGameMoneyServices.Account -= item.Price;
                }
                catch
                {
                    // Fallback if money services not available
                    return;
                }

                if (wcPlayer.AddItem(item))
                {
                    player.PlayLocalSound("sounds/buttons/button9.vsnd");
                    player.PrintToChat($" Bought {item.Name}");
                }
            });
        }

        MenuManager.OpenMainMenu(wcPlayer.Player, menu);
    }
}
