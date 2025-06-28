using CounterStrikeSharp.API.Core;
using System.Drawing;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Items;

internal class BootsOfSpeed : ShopItem
{
    internal override string Name => "Boots of Speed";
    internal override string Description => "Increase Speed by 20%";
    internal override int Price => 3000;

    internal override void Apply(CCSPlayerController player)
    {
        new BootsOfSpeedEffect(player).Start();
    }

    private class BootsOfSpeedEffect(CCSPlayerController owner) : WarcraftEffect(owner)
    {
        private float _originalModifier;

        public override void OnStart()
        {
            if (!Owner.IsAlive()) return;
            _originalModifier = Owner.PlayerPawn.Value.VelocityModifier;
            Owner.PlayerPawn.Value.VelocityModifier = _originalModifier * 1.2f;
        }

        public override void OnTick() { }

        public override void OnFinish()
        {
            if (!Owner.IsValid || !Owner.PawnIsAlive) return;
            Owner.PlayerPawn.Value.VelocityModifier = _originalModifier;
        }
    }
}
