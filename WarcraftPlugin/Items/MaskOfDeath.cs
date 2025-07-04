using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Helpers;
using System;

namespace WarcraftPlugin.Items;

internal class MaskOfDeath : ShopItem
{
    protected override string Name => "Mask of Death";
    protected override string Description => "50% Chance to Heal 15% of Weapon Damage dealt";
    internal override int Price => 4000;

    internal override void Apply(CCSPlayerController player) { }

    internal override void OnPlayerHurtOther(EventPlayerHurtOther @event)
    {
        if (@event.Attacker == null || !@event.Attacker.IsAlive()) return;

        if (Random.Shared.NextDouble() < 0.5)
        {
            var pawn = @event.Attacker.PlayerPawn.Value;
            var heal = (int)System.Math.Ceiling(@event.DmgHealth * 0.15f);
            var newHp = System.Math.Min(pawn.Health + heal, pawn.MaxHealth);
            @event.Attacker.SetHp((int)newHp);
        }
    }
}
