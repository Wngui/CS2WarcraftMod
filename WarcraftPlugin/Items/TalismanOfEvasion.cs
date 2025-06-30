using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;
using System;
namespace WarcraftPlugin.Items;

internal class TalismanOfEvasion : ShopItem
{
    internal override string Name => "Talisman of Evasion";
    internal override string Description => "20% chance to evade";
    internal override int Price => 4000;

    internal override void Apply(CCSPlayerController player) { }

    internal override void OnPlayerHurtOther(EventPlayerHurtOther @event)
    {
        if (Random.Shared.NextDouble() < 0.2)
        {
            @event.IgnoreDamage();
        }
    }
}
