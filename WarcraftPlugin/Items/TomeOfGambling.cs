using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System;

namespace WarcraftPlugin.Items;

internal class TomeOfGambling : ShopItem
{
    internal override string Name => "Tome of Gambling";
    internal override string Description => "Chance to gain 150-450 XP";
    internal override int Price => 8000;
    internal override bool IsInstant => true;

    internal override void Apply(CCSPlayerController player)
    {
        var xpGain = Random.Shared.Next(150, 451);
        WarcraftPlugin.Instance.XpSystem.AddXp(player, xpGain);
        player.PrintToChat($" {ChatColors.Gold}+{xpGain} XP{ChatColors.Default}");
    }
}
