using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API;
using System.Linq;
using WarcraftPlugin.Models;
using System.Drawing;
using WarcraftPlugin.Events;
using System.Collections.Generic;

namespace WarcraftPlugin.Classes
{
    internal class Paladin : WarcraftClass
    {
        private Timer _healingAuraTimer;
        private bool _hasUsedDivineResurrection = false;

        public override string DisplayName => "Paladin";
        public override Color DefaultColor => Color.Yellow;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Healing Aura", "Emit an aura that gradually heals nearby allies over time."),
            new WarcraftAbility("Holy Shield", "Surround yourself with a protective barrier that absorbs incoming damage."),
            new WarcraftAbility("Smite", "Infuse your attacks with divine energy, potentially stripping enemy armor."),
            new WarcraftAbility("Divine Resurrection", "Instantly revive a random fallen ally.")
        ];

        public override void Register()
        {
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookEvent<EventRoundStart>(RoundStart);

            HookAbility(3, Ultimate);
        }

        private void RoundStart(EventRoundStart start)
        {
            _hasUsedDivineResurrection = false;
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            if (WarcraftPlayer.GetAbilityLevel(0) > 0)
            {
                StartHealingAura();
            }

            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                Player.GiveNamedItem("item_assaultsuit");
                Player.SetArmor(Player.PlayerPawn.Value.ArmorValue + WarcraftPlayer.GetAbilityLevel(1) * 20);
            }
        }

        private void StartHealingAura()
        {
            _healingAuraTimer?.Kill();
            _healingAuraTimer = AddTimer(5f, () =>
            {
                if (!Player.IsValid())
                {
                    _healingAuraTimer?.Kill();
                    return;
                }

                var auraSize = WarcraftPlayer.GetAbilityLevel(0) * 100;
                var healingZone = Warcraft.CreateBoxAroundPoint(Player.PlayerPawn.Value.AbsOrigin, auraSize, auraSize, auraSize);
                //healingZone.Show(duration: 2); //Debug
                //Find players within area
                var playersToHeal = Utilities.GetPlayers().Where(x => x.Team == Player.Team && x.PawnIsAlive && Player.IsValid &&
                healingZone.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20)));

                if (playersToHeal.Any())
                {
                    foreach (var player in playersToHeal)
                    {
                        if (player.PlayerPawn.Value.Health < player.PlayerPawn.Value.MaxHealth)
                        {
                            var healthAfterHeal = player.PlayerPawn.Value.Health + WarcraftPlayer.GetAbilityLevel(0);
                            player.SetHp(Math.Min(healthAfterHeal, player.PlayerPawn.Value.MaxHealth));
                            Warcraft.SpawnParticle(player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 40), "particles/ui/ammohealthcenter/ui_hud_kill_burn_fire.vpcf", 1);
                        }
                    }
                }
            }, TimerFlags.REPEAT);
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1) return;

            if (!_hasUsedDivineResurrection)
            {
                DivineResurrection();
            }
            else
            {
                Player.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Red}Divine resurrection already used this round.{ChatColors.Default}", 1);
            }
        }

        private void DivineResurrection()
        {
            var deadTeamPlayers = Utilities.GetPlayers().Where(x => x.Team == Player.Team && !x.PawnIsAlive && Player.IsValid);

            // Check if there are any players on the same team
            if (deadTeamPlayers.Any())
            {
                _hasUsedDivineResurrection = true;
                // Generate a random index within the range of players
                int randomIndex = Random.Shared.Next(0, deadTeamPlayers.Count() - 1);

                // Get the random player
                var playerToRevive = deadTeamPlayers.ElementAt(randomIndex);

                //Revive
                playerToRevive.Respawn();
                playerToRevive.PlayerPawn.Value.Teleport(Player.CalculatePositionInFront(new Vector(10, 10, 60)), Player.PlayerPawn.Value.EyeAngles, new Vector());

                playerToRevive.PrintToChat(" " + $"{ChatColors.Green}You have been revived!{ChatColors.Default}");
                Utilities.GetPlayers().ForEach(x =>
                    x.PrintToChat(" " + $"{ChatColors.Green}{playerToRevive.PlayerName}{ChatColors.Default} has been revived by {Player.PlayerName}"));
            }
            else
            {
                Player.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Red}No allies fallen{ChatColors.Default}", 1);
            }
        }

        private void PlayerHurtOther(EventPlayerHurt @event)
        {
            var victim = @event.Userid;
            if (!victim.IsValid() || victim.UserId == Player.UserId) return;

            if (Warcraft.RollDice(WarcraftPlayer.GetAbilityLevel(2), 75))
            {
                if (victim.PlayerPawn.Value.ArmorValue > 0)
                {
                    victim.SetArmor(victim.PlayerPawn.Value.ArmorValue - WarcraftPlayer.GetAbilityLevel(2) * 5);
                    Warcraft.SpawnParticle(victim.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 40), "particles/survival_fx/gas_cannister_impact_child_flash.vpcf", 1);
                    Player.PlayLocalSound("sounds/weapons/taser/taser_hit.vsnd");
                }
            }
        }
    }
}