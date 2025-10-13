using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftPlugin.Events;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Core
{
    internal class XpSystem
    {
        private readonly WarcraftPlugin _plugin;
        private readonly Config _config;
        private readonly IStringLocalizer _localizer;
        private readonly EventSystem _eventSystem;


        internal XpSystem(WarcraftPlugin plugin, Config config, IStringLocalizer localizer, EventSystem eventSystem)
        {
            _plugin = plugin;
            _config = config;
            _localizer = localizer;
            _eventSystem = eventSystem;
        }

        private readonly List<int> _levelXpRequirement = [.. new int[256]];

        internal void GenerateXpCurve(int initial, float modifier, int maxLevel)
        {
            for (int i = 0; i <= maxLevel; i++)
            {
                if (i == 0)
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

            xpToAdd = _plugin.NewMethod(player, xpToAdd);

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
                player.PlayLocalSound("sounds/choseButton.vsnd");
                player.PlayLocalSound("sound/choseButton.vsnd");
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
            //TODO Я менял код
            int totalPointsUsed = 0;

            var abilityCount = wcPlayer.GetClass().Abilities.Count;
            for (int i = 0; i < abilityCount; i++)
            {
                totalPointsUsed += wcPlayer.GetAbilityLevel(i);
            }
            //TODO Добавил я проверка всего сколько уровней на прокачку если что изменить
            int level = wcPlayer.GetLevel();

            int maxPossiblePoints = Math.Min(level, 16);  // Ключевое изменение!
            if (level > WarcraftPlugin.UltLevel)
                level = WarcraftPlugin.MaxSkillLevel;

            if (totalPointsUsed >= maxPossiblePoints)
            {
                return 0;
            }
            int availablePoints = maxPossiblePoints - totalPointsUsed;

            return availablePoints;
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
            wcPlayer.Player.PrintToChat($"{ChatColors.Gold}[AutoSpell]: {ChatColors.Default}Таланты были распределены автоматически");
        }
        internal void CalculateAndAddKillXp(
            CCSPlayerController attacker,
            CCSPlayerController victim,
            string weaponName,
            bool headshot)
        {
            if (attacker == null || victim == null) return;

            var xpHeadshot = 0f;
            var xpKnife = 0f;
            //var xpFirstkill = 0f;

            if (headshot)
                xpHeadshot = Convert.ToInt32(_config.XpPerKill * _config.XpHeadshotModifier);

            if (weaponName.StartsWith("knife"))
            {
                xpKnife = Convert.ToInt32(_config.XpPerKill * _config.XpKnifeModifier);
            }
            // if (_eventSystem.firstkill == 0)
            // {
            //     xpFirstkill = Convert.ToInt32(_config.XpFirstKill);
            // }

            var xpToAdd = Convert.ToInt32(_config.XpPerKill + xpHeadshot + xpKnife);
            var levelBonus = 0;
            if (_config.EnableLevelDifferenceXp)
            {
                var attackerWc = _plugin.GetWcPlayer(attacker);
                var victimWc = _plugin.GetWcPlayer(victim);
                if (attackerWc != null && victimWc != null)
                {
                    var diff = victimWc.GetLevel() - attackerWc.GetLevel();
                    if (diff > 0)
                    {
                        var multiplier = 1 + (diff * 2f / (WarcraftPlugin.MaxLevel - 1));
                        var newXp = Convert.ToInt32(xpToAdd * multiplier);
                        levelBonus = newXp - xpToAdd;
                        xpToAdd = newXp;
                    }
                }
            }
            // var assistXp = Convert.ToInt32(_config.XpPerAssist);
            // if (assister != null && assister.IsValid && assister != attacker)
            // {
            //     AddXp(assister, assistXp);
            //     var shownAssist = _plugin.NewMethod(assister, assistXp);
            //     string assistText = $" {_localizer["xp.assist", shownAssist, victim.PlayerName]}";
            //     assister.PrintToChat(assistText);
            // }
            AddXp(attacker, xpToAdd);
            xpToAdd = _plugin.NewMethod(attacker, xpToAdd);
            _plugin.NewMethod1(attacker);
            if (_config.XpMultiply > 0)
            {
                xpToAdd = (int)((1 + _config.XpMultiply) * xpToAdd);
            }

            string hsBonus = xpHeadshot != 0 ? $"(+{xpHeadshot} {_localizer["xp.bonus.headshot"]})" : "";
            string knifeBonus = xpKnife != 0 ? $"(+{xpKnife} {_localizer["xp.bonus.knife"]})" : "";
            string levelDiffBonus = levelBonus > 0 ? $"(+{levelBonus} {_localizer["xp.bonus.level"]})" : "";
            //string firstkillBonus = xpFirstkill != 0 ? $"(+{xpFirstkill} {_localizer["xp.bonus.firstkill"]})" : "";

            string xpString = $" {_localizer["xp.kill", xpToAdd, victim.PlayerName, hsBonus, knifeBonus, levelDiffBonus]}";
            attacker.PrintToChat(xpString);
        }
    }
}
