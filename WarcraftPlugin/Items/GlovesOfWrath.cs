using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Items;

internal class GlovesOfWrath : ShopItem
{
    internal override string Name => "Gloves of Wrath";
    internal override string Description => "Receive an HE grenade every 12s";
    internal override int Price => 3000;

    internal override void Apply(CCSPlayerController player)
    {
        new GrenadeSupplyEffect(player, "weapon_hegrenade", "HE grenade").Start();
    }

    private class GrenadeSupplyEffect(CCSPlayerController owner, string grenadeName, string displayName)
        : WarcraftEffect(owner, onTickInterval: 12f)
    {
        private readonly string _grenadeName = grenadeName;
        private readonly string _displayName = displayName;

        public override void OnStart() { }

        public override void OnTick()
        {
            if (!Owner.IsAlive()) return;
            var services = Owner.PlayerPawn.Value.WeaponServices;
            if (services?.MyWeapons == null) return;
            bool hasGrenade = services.MyWeapons.Any(w => w.Value?.DesignerName == _grenadeName);
            if (!hasGrenade)
            {
                Owner.GiveNamedItem(_grenadeName);
                Owner.PrintToChat($" {ChatColors.Green}+{_displayName}{ChatColors.Default}");
            }
        }

        public override void OnFinish() { }
    }
}
