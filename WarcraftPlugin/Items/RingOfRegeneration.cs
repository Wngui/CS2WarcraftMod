using CounterStrikeSharp.API.Core;
using System.Drawing;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Items;

internal class RingOfRegeneration : ShopItem
{
    protected override string Name => "Ring of Regeneration";
    protected override string Description => "Regen 1 HP each sec.";
    internal override int Price { get; set; } = 3000;
    internal override Color Color { get; set; } = Color.FromArgb(255, 50, 205, 50); // LimeGreen for regeneration/healing

    internal int RegenPerSecond { get; set; } = 1;

    internal override void Apply(CCSPlayerController player)
    {
        new RingOfRegenerationEffect(player, RegenPerSecond).Start();
    }

    private class RingOfRegenerationEffect(CCSPlayerController owner, int regenPerSecond) : WarcraftEffect(owner, onTickInterval: 1f)
    {
        public override void OnStart() { }

        public override void OnTick()
        {
            if (!Owner.IsAlive()) return;
            var pawn = Owner.PlayerPawn.Value;
            if (pawn.Health < pawn.MaxHealth)
            {
                Owner.SetHp(pawn.Health + regenPerSecond);
            }
        }

        public override void OnFinish() { }
    }
}
