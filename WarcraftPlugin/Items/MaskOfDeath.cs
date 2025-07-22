using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Helpers;
using System;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class MaskOfDeath : ShopItem
{
    protected override string Name => "Mask of Death";
    protected override string Description => "50% Chance to Heal 15% of Weapon Damage dealt";
    internal override int Price { get; set; } = 4000;
    internal override Color Color { get; set; } = Color.FromArgb(255, 220, 20, 60); // Crimson for lifesteal/offensive

    internal double LifeStealChance { get; set; } = 0.5;
    internal double LifeStealPercent { get; set; } = 0.15;

    internal override void Apply(CCSPlayerController player) { }

    internal override void OnPlayerHurtOther(EventPlayerHurtOther @event)
    {
        if (@event.Attacker == null || !@event.Attacker.IsAlive()) return;

        if (Random.Shared.NextDouble() < LifeStealChance)
        {
            var pawn = @event.Attacker.PlayerPawn.Value;
            var heal = (int)Math.Ceiling(@event.DmgHealth * LifeStealPercent);
            var newHp = Math.Min(pawn.Health + heal, pawn.MaxHealth);
            @event.Attacker.SetHp((int)newHp);
        }
    }
}
