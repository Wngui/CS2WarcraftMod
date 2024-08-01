using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using System;
using System.Numerics;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Cooldowns
{
    public class CooldownManager
    {
        private readonly float _tickRate = 0.25f;

        public void Initialize()
        {
            WarcraftPlugin.Instance.AddTimer(_tickRate, CooldownTick, TimerFlags.REPEAT);
        }

        private void CooldownTick()
        {
            foreach (var player in WarcraftPlugin.Instance.Players)
            {
                if (player == null) continue;
                for (int i = 0; i < player.AbilityCooldowns.Count; i++)
                {
                    if (player.AbilityCooldowns[i] <= 0) continue;

                    player.AbilityCooldowns[i] -= 0.25f;

                    if (player.AbilityCooldowns[i] <= 0)
                    {
                        PlayEffects(player, i);
                    }
                }
            }
        }

        public static bool IsAvailable(WarcraftPlayer player, int abilityIndex)
        {
            return player.AbilityCooldowns[abilityIndex] <= 0;
        }

        public static float Remaining(WarcraftPlayer player, int abilityIndex)
        {
            return player.AbilityCooldowns[abilityIndex];
        }

        public static void StartCooldown(WarcraftPlayer player, int abilityIndex, float abilityCooldown)
        {
            player.AbilityCooldowns[abilityIndex] = abilityCooldown;
        }

        public static void ResetCooldowns(WarcraftPlayer player)
        {
            for (int abilityIndex = 0; abilityIndex < player.AbilityCooldowns.Count; abilityIndex++)
            {
                player.AbilityCooldowns[abilityIndex] = 0;
            }
        }

        private static void PlayEffects(WarcraftPlayer wcplayer, int abilityIndex)
        {
            var ability = wcplayer.GetClass().GetAbility(abilityIndex);

            wcplayer.GetPlayer().PlaySound("sounds/weapons/taser/taser_charge_ready.vsnd");

            wcplayer.GetPlayer().PrintToCenter($"{ability.DisplayName} ready");
        }
    }
}