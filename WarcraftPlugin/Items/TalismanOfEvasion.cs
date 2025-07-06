using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;
using System;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class TalismanOfEvasion : ShopItem
{
    protected override string Name => "Talisman of Evasion";
    protected override string Description => "20% chance to evade";
    internal override int Price => 4000;
    internal override Color Color => Color.FromArgb(255, 138, 43, 226); // BlueViolet for evasion/rare

    internal override void Apply(CCSPlayerController player) { }

    internal override void OnPlayerHurtOther(EventPlayerHurtOther @event)
    {
        if (Random.Shared.NextDouble() < 0.2)
        {
            @event.IgnoreDamage();
        }
    }
}
