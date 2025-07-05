using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class TomeOfExperience : ShopItem
{
    protected override string Name => "Tome of Experience";
    protected override string Description => "Gain 150XP";
    internal override int Price => 4000;
    internal override bool IsInstant => true;
    internal override Color Color => Color.FromArgb(255, 255, 215, 0); // Gold for legendary/XP/instant

    internal override void Apply(CCSPlayerController player)
    {
        const int xpGain = 150;
        WarcraftPlugin.Instance.XpSystem.AddXp(player, xpGain);
        player.PrintToChat($" {ChatColors.Gold}+{xpGain} XP{ChatColors.Default}");
    }
}

