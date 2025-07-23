using CounterStrikeSharp.API.Core;
using System;
using System.Drawing;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Items;

internal class BootsOfSpeed : ShopItem
{
    protected override string Name => "Boots of Speed";
    protected override FormattableString Description => $"Increase Speed by {(SpeedModifier-1)*100}%";
    internal override int Price { get; set; } = 2500;
    internal override Color Color { get; set; } = Color.FromArgb(255, 30, 144, 255); // DodgerBlue for speed/movement

    [Configurable]
    public float SpeedModifier { get; set; } = 1.2f; // 20% speed increase

    internal override void Apply(CCSPlayerController player)
    {
        new BootsOfSpeedEffect(player, SpeedModifier).Start();
    }

    private class BootsOfSpeedEffect(CCSPlayerController owner, float speedModifier) : WarcraftEffect(owner)
    {
        private float _originalModifier;

        public override void OnStart()
        {
            if (!Owner.IsAlive()) return;
            _originalModifier = Owner.PlayerPawn.Value.VelocityModifier;
            Owner.PlayerPawn.Value.VelocityModifier = _originalModifier * speedModifier;
        }

        public override void OnTick() { }

        public override void OnFinish()
        {
            if (!Owner.IsValid || !Owner.PawnIsAlive) return;
            Owner.PlayerPawn.Value.VelocityModifier = _originalModifier;
        }
    }
}
