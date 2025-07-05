using CounterStrikeSharp.API.Modules.Timers;
using System.Linq;
using System.Collections.Generic;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Core
{
    internal class CooldownManager
    {
        private readonly float _tickRate = 0.25f;
        private readonly HashSet<WarcraftPlayer> _playersOnCooldown = [];

        internal void Initialize()
        {
            WarcraftPlugin.Instance.AddTimer(_tickRate, CooldownTick, TimerFlags.REPEAT);
        }

        private void RegisterPlayer(WarcraftPlayer player)
        {
            if (player != null)
            {
                _playersOnCooldown.Add(player);
            }
        }

        private void UnregisterPlayer(WarcraftPlayer player)
        {
            if (player != null)
            {
                _playersOnCooldown.Remove(player);
            }
        }

        private void CooldownTick()
        {
            foreach (var wcPlayer in _playersOnCooldown.ToArray())
            {
                bool hasCooldown = false;

                for (int i = 0; i < wcPlayer.AbilityCooldowns.Count; i++)
                {
                    if (wcPlayer.AbilityCooldowns[i] <= 0) continue;

                    wcPlayer.AbilityCooldowns[i] -= _tickRate;

                    if (wcPlayer.AbilityCooldowns[i] <= 0)
                    {
                        PlayEffects(wcPlayer, i);
                    }
                    else
                    {
                        hasCooldown = true;
                    }
                }

                if (!hasCooldown)
                {
                    _playersOnCooldown.Remove(wcPlayer);
                }
            }
        }

        internal static bool IsAvailable(WarcraftPlayer player, int abilityIndex)
        {
            return player.AbilityCooldowns[abilityIndex] <= 0;
        }

        internal static float Remaining(WarcraftPlayer player, int abilityIndex)
        {
            return player.AbilityCooldowns[abilityIndex];
        }

        internal static void StartCooldown(WarcraftPlayer player, int abilityIndex, float abilityCooldown)
        {
            player.AbilityCooldowns[abilityIndex] = abilityCooldown;
            WarcraftPlugin.Instance.CooldownManager.RegisterPlayer(player);
        }

        internal static void ResetCooldowns(WarcraftPlayer player)
        {
            for (int abilityIndex = 0; abilityIndex < player.AbilityCooldowns.Count; abilityIndex++)
            {
                player.AbilityCooldowns[abilityIndex] = 0;
            }

            WarcraftPlugin.Instance.CooldownManager.UnregisterPlayer(player);
        }

        private static void PlayEffects(WarcraftPlayer wcplayer, int abilityIndex)
        {
            var ability = wcplayer.GetClass().GetAbility(abilityIndex);

            wcplayer.GetPlayer().PlayLocalSound("sounds/weapons/taser/taser_charge_ready.vsnd");
            wcplayer.GetPlayer().PrintToCenter(WarcraftPlugin.Instance.Localizer["ability.ready", ability.DisplayName]);
        }
    }
}
