using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class SockOfFeathers : ShopItem
{
    protected override string Name => "Sock of Feathers";
    protected override string Description => "Decrease Gravity by 50%";
    internal override int Price => 1500;
    internal override Color Color => Color.FromArgb(255, 0, 206, 209); // DarkTurquoise for movement/utility

    internal override void Apply(CCSPlayerController player)
    {
        new SockOfFeathersEffect(player).Start();
    }

    private class SockOfFeathersEffect(CCSPlayerController owner) : WarcraftEffect(owner)
    {
        private float _originalGravityScale;

        public override void OnStart()
        {
            if (!Owner.IsAlive()) return;
            _originalGravityScale = Owner.PlayerPawn.Value.GravityScale;
            Owner.PlayerPawn.Value.GravityScale = _originalGravityScale * 0.5f;
        }

        public override void OnTick() { }

        public override void OnFinish()
        {
            if (!Owner.IsValid || !Owner.PawnIsAlive) return;
            Owner.PlayerPawn.Value.GravityScale = _originalGravityScale;
        }
    }
}
