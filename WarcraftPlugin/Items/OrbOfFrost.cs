using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;
using System;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class OrbOfFrost : ShopItem
{
    protected override string Name => "Orb of Frost";
    protected override FormattableString Description => $"{SlowChance*100}% chance to slow enemy on hit";
    internal override int Price { get; set; } = 3500;
    internal override Color Color { get; set; } = Color.FromArgb(255, 0, 191, 255); // DeepSkyBlue for frost/slow

    [Configurable]
    internal float SlowChance { get; set; } = 0.33f;
    [Configurable]
    internal float SlowModifier { get; set; } = 0.67f;
    [Configurable]
    internal float SlowDuration { get; set; } = 2.0f; // Duration in seconds

    internal override void Apply(CCSPlayerController player) { }

    internal override void OnPlayerHurtOther(EventPlayerHurtOther @event)
    {
        if (@event.Attacker == null || !@event.Attacker.IsAlive()) return;
        if (@event.Userid == null || !@event.Userid.IsAlive()) return;

        if (Random.Shared.NextDouble() < SlowChance)
        {
            new FrostSlowEffect(@event.Attacker, @event.Userid, SlowDuration, SlowModifier).Start();
        }
    }

    private class FrostSlowEffect(CCSPlayerController owner, CCSPlayerController victim, float duration, float slowModifier)
        : WarcraftEffect(owner, duration)
    {
        private readonly CCSPlayerController _victim = victim;
        private float _originalSpeed;
        private float _originalModifier;

        public override void OnStart()
        {
            if (!_victim.IsAlive()) return;
            var pawn = _victim.PlayerPawn.Value;
            _originalSpeed = pawn.MovementServices.Maxspeed;
            _originalModifier = pawn.VelocityModifier;
            pawn.MovementServices.Maxspeed = _originalSpeed * slowModifier;
            pawn.VelocityModifier = _originalModifier * slowModifier;
            _victim.PrintToChat($" {ShopItem.Localizer["item.orb_of_frost.slowed", Owner.PlayerName]}");
        }

        public override void OnTick() { }

        public override void OnFinish()
        {
            if (!_victim.IsValid || !_victim.PawnIsAlive) return;
            var pawn = _victim.PlayerPawn.Value;
            pawn.MovementServices.Maxspeed = _originalSpeed;
            pawn.VelocityModifier = _originalModifier;
        }
    }
}
