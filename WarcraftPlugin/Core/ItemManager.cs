using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Items;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Core
{
    internal static class ItemManager
    {
        public static void ApplyItems(this WarcraftPlayer warcraftPlayer)
        {
            if (warcraftPlayer?.Items == null)
                return;

            foreach (var item in warcraftPlayer.Items.ToList())
            {
                item.Apply(warcraftPlayer.Player);
            }
        }

        public static void OnPlayerHurtOther(EventPlayerHurtOther hurtOtherEvent)
        {
            var attacker = hurtOtherEvent.Attacker;
            var items = attacker?.GetWarcraftPlayer()?.Items;
            if (items != null)
            {
                foreach (var item in items)
                {
                    item.OnPlayerHurtOther(hurtOtherEvent);
                }
            }
        }

        public static bool AddItem(this WarcraftPlayer warcraftPlayer, ShopItem item)
        {
            if (warcraftPlayer.Items.Any(inv => inv.GetType() == item.GetType()))
                return false;

            warcraftPlayer.Items.Add(item);
            return true;
        }

        public static void ClearItems(this WarcraftPlayer warcraftPlayer)
        {
            var items = warcraftPlayer?.Items;
            items?.Clear();
        }

        public static void PrintItemsOwned(this WarcraftPlayer warcraftPlayer)
        {
            var player = warcraftPlayer?.Player;
            string itemsOwned = $" {ChatColors.Grey}None";
            if (warcraftPlayer != null && warcraftPlayer.Items != null && warcraftPlayer.Items.Count > 0)
            {
                itemsOwned = string.Join(", ", warcraftPlayer.Items.Select(i =>
                    $"{ChatColors.Green}[{ChatColors.Gold}{i.LocalizedName}{ChatColors.Green}]"));
            }
            player?.PrintToChat($" Items: {itemsOwned}");
        }
    }
}
