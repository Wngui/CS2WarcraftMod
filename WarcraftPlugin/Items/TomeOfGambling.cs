using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class TomeOfGambling : ShopItem
{
    protected override string Name => "Tome of Gambling";
    protected override string Description => "Chance to gain 150-450 XP";
    internal override int Price { get; set; } = 8000;
    internal override bool IsInstant => true;
    internal override Color Color { get; set; } = Color.RosyBrown; 

    internal int MinXpGain { get; set; } = 150;
    internal int MaxXpGain { get; set; } = 450;

    internal override void Apply(CCSPlayerController player)
    {
        var xpGain = Random.Shared.Next(MinXpGain, MaxXpGain);
        WarcraftPlugin.Instance.XpSystem.AddXp(player, xpGain);
        player.PrintToChat($" {ChatColors.Gold}+{xpGain} XP{ChatColors.Default}");
    }
}
