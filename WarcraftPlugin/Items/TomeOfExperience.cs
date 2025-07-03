using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WarcraftPlugin.Items;

internal class TomeOfExperience : ShopItem
{
    internal override string Name => "Tome of Experience";
    internal override string Description => "Gain 150XP";
    internal override int Price => 4000;
    internal override bool IsInstant => true;

    internal override void Apply(CCSPlayerController player)
    {
        const int xpGain = 150;
        WarcraftPlugin.Instance.XpSystem.AddXp(player, xpGain);
        player.PrintToChat($" {ChatColors.Gold}+{xpGain} XP{ChatColors.Default}");
    }
}

