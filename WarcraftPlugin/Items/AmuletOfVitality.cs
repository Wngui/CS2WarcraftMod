using CounterStrikeSharp.API.Core;
using System.Drawing;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Items;

internal class AmuletOfVitality : ShopItem
{
    protected override string Name => "Amulet of Vitality";
    protected override string Description => "Increase max HP by 50";
    internal override int Price => 3500;
    internal override Color Color => Color.FromArgb(255, 255, 69, 0); // OrangeRed for vitality/health

    internal override void Apply(CCSPlayerController player)
    {
        new AmuletOfVitalityEffect(player).Start();
    }

    private class AmuletOfVitalityEffect(CCSPlayerController owner) : WarcraftEffect(owner)
    {
        private int _originalMaxHealth;

        public override void OnStart()
        {
            if (!Owner.IsAlive()) return;
            var pawn = Owner.PlayerPawn.Value;
            _originalMaxHealth = pawn.MaxHealth;
            pawn.MaxHealth = _originalMaxHealth + 50;
            var newHealth = pawn.Health + 50;
            Owner.SetHp((int)newHealth);
        }

        public override void OnTick() { }

        public override void OnFinish()
        {
            if (!Owner.IsValid || !Owner.PawnIsAlive) return;
            var pawn = Owner.PlayerPawn.Value;
            pawn.MaxHealth = _originalMaxHealth;
            if (pawn.Health > pawn.MaxHealth)
            {
                Owner.SetHp(pawn.MaxHealth);
            }
        }
    }
}
