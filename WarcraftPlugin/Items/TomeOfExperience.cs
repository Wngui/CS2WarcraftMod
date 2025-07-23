using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class TomeOfExperience : ShopItem
{
    protected override string Name => "Tome of Experience";
    protected override FormattableString Description => $"Gain {XpGain}XP";
    internal override int Price { get; set; } = 4000;
    internal override bool IsInstant => true;
    internal override Color Color { get; set; } = Color.Brown;

    [Configurable]
    internal int XpGain { get; set; } = 50;

    internal override void Apply(CCSPlayerController player)
    {
        WarcraftPlugin.Instance.XpSystem.AddXp(player, XpGain);
        player.PrintToChat($" {ChatColors.Gold}+{XpGain} XP{ChatColors.Default}");
    }
}

