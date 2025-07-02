using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;
using System;
using CounterStrikeSharp.API.Modules.Utils;
namespace WarcraftPlugin.Items;

internal class OrbOfFrost : ShopItem
{
    internal override string Name => "Orb of Frost";
    internal override string Description => "33% chance to slow enemy on hit";
    internal override int Price => 3500;

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
            _victim.PrintToChat($" {ChatColors.Red}Slowed by {ChatColors.Green}{Owner.PlayerName}");
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
