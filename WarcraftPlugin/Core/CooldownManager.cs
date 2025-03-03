using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Core
{
    internal class CooldownManager
    {
        private readonly float _tickRate = 0.25f;

        internal void Initialize()
        {
            WarcraftPlugin.Instance.AddTimer(_tickRate, CooldownTick, TimerFlags.REPEAT);
        }

        private void CooldownTick()
        {
            foreach (var player in Utilities.GetPlayers())
            {
                var warcraftPlayer = player?.GetWarcraftPlayer();
                if (warcraftPlayer == null) continue;
                for (int i = 0; i < warcraftPlayer.AbilityCooldowns.Count; i++)
                {
                    if (warcraftPlayer.AbilityCooldowns[i] <= 0) continue;

                    warcraftPlayer.AbilityCooldowns[i] -= 0.25f;

                    if (warcraftPlayer.AbilityCooldowns[i] <= 0)
                    {
                        PlayEffects(warcraftPlayer, i);
                    }
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
        }

        internal static void ResetCooldowns(WarcraftPlayer player)
        {
            for (int abilityIndex = 0; abilityIndex < player.AbilityCooldowns.Count; abilityIndex++)
            {
                player.AbilityCooldowns[abilityIndex] = 0;
            }
        }

        private static void PlayEffects(WarcraftPlayer wcplayer, int abilityIndex)
        {
            var ability = wcplayer.GetClass().GetAbility(abilityIndex);

            wcplayer.GetPlayer().PlayLocalSound("sounds/weapons/taser/taser_charge_ready.vsnd");
            wcplayer.GetPlayer().PrintToCenter(WarcraftPlugin.Instance.Localizer["ability.ready", ability.DisplayName]);
        }
    }
}