using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class TomeOfExperience : ShopItem
{
    protected override string Name => "Tome of Experience";
    protected override string Description => "Gain 150XP";
    internal override int Price { get; set; } = 4000;
    internal override bool IsInstant => true;
    internal override Color Color { get; set; } = Color.Brown;

    [Configurable]
    internal int XpGain { get; set; } = 150;

    internal override void Apply(CCSPlayerController player)
    {
        WarcraftPlugin.Instance.XpSystem.AddXp(player, XpGain);
        player.PrintToChat($" {ChatColors.Gold}+{XpGain} XP{ChatColors.Default}");
    }
}

