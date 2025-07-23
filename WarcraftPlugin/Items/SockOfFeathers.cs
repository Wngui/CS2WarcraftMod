using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;
using System.Drawing;
using System;

namespace WarcraftPlugin.Items;

internal class SockOfFeathers : ShopItem
{
    protected override string Name => "Sock of Feathers";
    protected override FormattableString Description => $"Decrease Gravity by {(1 - GravityModifier) * 100}%";
    internal override int Price { get; set; } = 1500;
    internal override Color Color { get; set; } = Color.FromArgb(255, 0, 206, 209); // DarkTurquoise for movement/utility

    [Configurable]
    internal float GravityModifier { get; set; } = 0.5f;

    internal override void Apply(CCSPlayerController player)
    {
        new SockOfFeathersEffect(player, GravityModifier).Start();
    }

    private class SockOfFeathersEffect(CCSPlayerController owner, float gravityModifier) : WarcraftEffect(owner)
    {
        private float _originalGravityScale;

        public override void OnStart()
        {
            if (!Owner.IsAlive()) return;
            _originalGravityScale = Owner.PlayerPawn.Value.GravityScale;
            Owner.PlayerPawn.Value.GravityScale = _originalGravityScale * gravityModifier;
        }

        public override void OnTick() { }

        public override void OnFinish()
        {
            if (!Owner.IsValid || !Owner.PawnIsAlive) return;
            Owner.PlayerPawn.Value.GravityScale = _originalGravityScale;
        }
    }
}
