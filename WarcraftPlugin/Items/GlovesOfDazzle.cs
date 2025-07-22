using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System.Linq;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Items;

internal class GlovesOfDazzle : ShopItem
{
    protected override string Name => "Gloves of Dazzle";
    protected override string Description => "Receive a Flashbang every 12s";
    internal override int Price { get; set; } = 3000;
    internal override Color Color { get; set; } = Color.FromArgb(255, 255, 255, 0); // Yellow for utility/flashbang

    internal float GrenadeInterval { get; set; } = 12f;
    internal string GrenadeType { get; set; } = "weapon_flashbang";

    internal override void Apply(CCSPlayerController player)
    {
        new GrenadeSupplyEffect(player, "weapon_flashbang", GrenadeInterval, Localizer["item.gloves_of_dazzle.grenade_name"]).Start();
    }

    private class GrenadeSupplyEffect(CCSPlayerController owner, string grenadeName, float grenadeInterval, string displayName)
        : WarcraftEffect(owner, onTickInterval: grenadeInterval)
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
