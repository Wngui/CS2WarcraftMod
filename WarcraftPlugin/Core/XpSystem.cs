using CounterStrikeSharp.API.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Core
{
    internal class XpSystem
    {
        private readonly WarcraftPlugin _plugin;

        internal XpSystem(WarcraftPlugin plugin)
        {
            _plugin = plugin;
        }

        private readonly List<int> _levelXpRequirement = new(new int[256]);

        internal void GenerateXpCurve(int initial, float modifier, int maxLevel)
        {
            for (int i = 1; i <= maxLevel; i++)
            {
                if (i == 1)
                    _levelXpRequirement[i] = initial;
                else
                    _levelXpRequirement[i] = Convert.ToInt32(_levelXpRequirement[i - 1] * modifier);
            }
        }

        internal int GetXpForLevel(int level)
        {
            return _levelXpRequirement[level];
        }

        internal void AddXp(CCSPlayerController player, int xpToAdd)
        {
            var wcPlayer = _plugin.GetWcPlayer(player);
            if (wcPlayer == null) return;

            if (wcPlayer.GetLevel() >= WarcraftPlugin.MaxLevel) return;

            wcPlayer.currentXp += xpToAdd;

            while (wcPlayer.currentXp >= wcPlayer.amountToLevel)
            {
                wcPlayer.currentXp = wcPlayer.currentXp - wcPlayer.amountToLevel;
                GrantLevel(wcPlayer);

                if (wcPlayer.GetLevel() >= WarcraftPlugin.MaxLevel) return;
            }
        }

        internal void GrantLevel(WarcraftPlayer wcPlayer)
        {
            if (wcPlayer.GetLevel() >= WarcraftPlugin.MaxLevel) return;

            wcPlayer.currentLevel += 1;

            RecalculateXpForLevel(wcPlayer);
            PerformLevelupEvents(wcPlayer);
            if (wcPlayer.GetPlayer().IsBot)
            {
                AutoSpendSkillPoints(wcPlayer);
            }
        }

        private static void PerformLevelupEvents(WarcraftPlayer wcPlayer)
        {
            var player = wcPlayer.GetPlayer();
            if (player.IsAlive())
            {
                player.PlayLocalSound("play sounds/ui/achievement_earned.vsnd");
                Warcraft.SpawnParticle(player.PlayerPawn.Value.AbsOrigin, "particles/ui/ammohealthcenter/ui_hud_kill_streaks_glow_5.vpcf", 1);
            }

            WarcraftPlugin.RefreshPlayerName(player);
        }

        internal void RecalculateXpForLevel(WarcraftPlayer wcPlayer)
        {
            if (wcPlayer.currentLevel == WarcraftPlugin.MaxLevel)
            {
                wcPlayer.amountToLevel = 0;
                return;
            }

            wcPlayer.amountToLevel = GetXpForLevel(wcPlayer.currentLevel);
        }

        internal static int GetFreeSkillPoints(WarcraftPlayer wcPlayer)
        {
            int totalPointsUsed = 0;

            var abilityCount = wcPlayer.GetClass().Abilities.Count;
            for (int i = 0; i < abilityCount; i++)
            {
                totalPointsUsed += wcPlayer.GetAbilityLevel(i);
            }

            int level = wcPlayer.GetLevel();
            if (level > WarcraftPlugin.MaxLevel)
                level = WarcraftPlugin.MaxSkillLevel;

            return level - totalPointsUsed;
        }

        private static readonly Random _random = new();

        internal static void AutoSpendSkillPoints(WarcraftPlayer wcPlayer)
        {
            var wcClass = wcPlayer.GetClass();
            while (GetFreeSkillPoints(wcPlayer) > 0)
            {
                var available = Enumerable.Range(0, wcClass.Abilities.Count)
                    .Where(i => wcPlayer.GetAbilityLevel(i) < WarcraftPlayer.GetMaxAbilityLevel(i)
                                && (i != WarcraftPlayer.UltimateAbilityIndex || wcPlayer.IsMaxLevel))
                    .ToList();
                if (available.Count == 0)
                    break;
                var index = available[_random.Next(available.Count)];
                wcPlayer.GrantAbilityLevel(index);
            }
        }
    }
}
