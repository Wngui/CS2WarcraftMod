using CounterStrikeSharp.API;
using g3;
using System.Linq;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Items;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Menu.WarcraftMenu;

internal static class ServerMenu
{
    internal static void Show(WarcraftPlayer wcPlayer)
    {
        var menu = MenuManager.CreateMenu($"<font color='lightgrey' class='{FontSizes.FontSizeM}'>" + ShopItem.Localizer["menu.shop"] + "</font>", 4);
        var plugin = WarcraftPlugin.Instance;

        var items = Shop.Items;

        menu.Add(
                "<font color='white' class='" + FontSizes.FontSizeSm + "'>Наш Discord</font>",
                "<font color='#D3D3D3' class='" + FontSizes.FontSizeS + "'>Нажмите, чтобы перейти в наш Discord</font>",
                (p, opt) =>
                {
                    // Когда игрок выбирает этот пункт, он получает информацию о Discord
                    p.PrintToChat("Привет! Вот наш Discord: https://discord.gg/mCdakBSgpD");
                }
            );
        menu.Add(
                "<font color='white' class='" + FontSizes.FontSizeSm + "'>Выбрать расу</font>",
                "<font color='#D3D3D3' class='" + FontSizes.FontSizeS + "'>Меню Классов</font>",
                (p, opt) =>
                {
                    p.PrintToChat("Пока не работает пиши - !class");
                }
            );
        menu.Add(
                "<font color='white' class='" + FontSizes.FontSizeSm + "'>Меню скиллов</font>",
                "<font color='#D3D3D3' class='" + FontSizes.FontSizeS + "'>Список скиллов расы</font>",
                (p, opt) =>
                {
                    p.PrintToChat("Пока не работает пиши - !skills");
                }
            );
        menu.Add(
                "<font color='white' class='" + FontSizes.FontSizeSm + "'>Автораспределние талантов</font>",
                "<font color='#D3D3D3' class='" + FontSizes.FontSizeS + "'>Расспределить скиллов</font>",
                (p, opt) =>
                {
                    p.PrintToChat("Пиши в чат !as и все таланты сами распределятся");
                }
            );
        menu.Add(
                "<font color='white' class='" + FontSizes.FontSizeSm + "'>Пожаловаться на игрока</font>",
                "<font color='#D3D3D3' class='" + FontSizes.FontSizeS + "'>Пожаловаться на игрока</font>",
                (p, opt) =>
                {
                    p.PrintToChat("Пиши в чат !report пожаловаться на игрока");
                }
            );
        MenuManager.OpenMainMenu(wcPlayer.Player, menu);
    }
}
