using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;
using System;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class TalismanOfEvasion : ShopItem
{
    protected override string Name => "Talisman of Evasion";
    protected override string Description => "20% chance to evade";
    internal override int Price { get; set; } = 4000;
    internal override Color Color { get; set; } = Color.FromArgb(255, 138, 43, 226); // BlueViolet for evasion/rare

    [Configurable]
    internal double EvasionChance { get; set; } = 0.2; // 20% chance to evade

    internal override void Apply(CCSPlayerController player) { }

    internal override void OnPlayerHurtOther(EventPlayerHurtOther @event)
    {
        if (Random.Shared.NextDouble() < EvasionChance)
        {
            @event.IgnoreDamage();
        }
    }
}
