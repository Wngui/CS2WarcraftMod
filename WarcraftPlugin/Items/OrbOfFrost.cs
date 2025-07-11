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
    protected override string Description => "33% chance to slow enemy on hit";
    internal override int Price => 3500;
    internal override Color Color => Color.FromArgb(255, 0, 191, 255); // DeepSkyBlue for frost/slow

    internal override void Apply(CCSPlayerController player) { }

    internal override void OnPlayerHurtOther(EventPlayerHurtOther @event)
    {
        if (@event.Attacker == null || !@event.Attacker.IsAlive()) return;
        if (@event.Userid == null || !@event.Userid.IsAlive()) return;

        if (Random.Shared.NextDouble() < 0.33)
        {
            new FrostSlowEffect(@event.Attacker, @event.Userid, 2f).Start();
        }
    }

    private class FrostSlowEffect(CCSPlayerController owner, CCSPlayerController victim, float duration)
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
            pawn.MovementServices.Maxspeed = _originalSpeed * 0.67f;
            pawn.VelocityModifier = _originalModifier * 0.67f;
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
