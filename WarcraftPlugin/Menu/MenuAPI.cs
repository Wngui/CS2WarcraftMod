using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Collections.Generic;
using System.Linq;

namespace WarcraftPlugin.Menu;

public static class MenuAPI
{

    public static readonly Dictionary<int, MenuPlayer> Players = [];

    public static void Load(BasePlugin plugin, bool hotReload)
    {
        MenuPlayer.Localizer = plugin.Localizer;

        plugin.RegisterEventHandler<EventPlayerActivate>((@event, info) =>
        {
            if (@event.Userid != null)
                Players[@event.Userid.Slot] = new MenuPlayer
                {
                    player = @event.Userid,
                    Buttons = 0
                };
            return HookResult.Continue;
        });

        plugin.RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            if (@event.Userid != null) Players.Remove(@event.Userid.Slot);
            return HookResult.Continue;
        });

        plugin.RegisterListener<Listeners.OnTick>(OnTick);

        if (hotReload)
            foreach (var pl in Utilities.GetPlayers())
            {
                Players[pl.Slot] = new MenuPlayer
                {
                    player = pl,
                    Buttons = pl.Buttons
                };
            }
    }

    public static void OnTick()
    {
        foreach (var player in Players.Values.Where(p => p.MainMenu != null))
        {
            if ((player.Buttons & PlayerButtons.Forward) == 0 && (player.player.Buttons & PlayerButtons.Forward) != 0)
            {
                player.ScrollUp();
            }
            else if ((player.Buttons & PlayerButtons.Back) == 0 && (player.player.Buttons & PlayerButtons.Back) != 0)
            {
                player.ScrollDown();
            }
            else if ((player.Buttons & PlayerButtons.Jump) == 0 && (player.player.Buttons & PlayerButtons.Jump) != 0)
            {
                player.Choose();
            }
            else if ((player.Buttons & PlayerButtons.Use) == 0 && (player.player.Buttons & PlayerButtons.Use) != 0)
            {
                player.Choose();
            }

            if (((long)player.player.Buttons & 8589934592) == 8589934592)
            {
                player.OpenMainMenu(null);
            }

            player.Buttons = player.player.Buttons;
            if (player.CenterHtml != "")
                Server.NextFrame(() =>
                player.player.PrintToCenterHtml(player.CenterHtml)
            );
        }
    }


}
