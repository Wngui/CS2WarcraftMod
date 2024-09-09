using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API;
using System.Linq;
using WarcraftPlugin.Models;
using System.Drawing;

namespace WarcraftPlugin.Classes
{
    public class Paladin : WarcraftClass
    {
        private Timer _healingAuraTimer;
        private bool _hasUsedDivineResurrection = false;

        public override string InternalName => "paladin";
        public override string DisplayName => "Paladin";
        public override DefaultClassModel DefaultModel => new()
        {
            //TModel = "characters/models/tm_phoenix_heavy/tm_phoenix_heavy.vmdl",
            //CTModel = "characters/models/ctm_heavy/ctm_heavy.vmdl"
        };
        public override Color DefaultColor => Color.Yellow;

        public override void Register()
        {
            AddAbility(new WarcraftAbility("healing_aura", "Healing Aura",
                i => $"{ChatColors.BlueGrey}Emit an aura that gradually {ChatColors.Green}heals{ChatColors.BlueGrey} nearby allies over time."));

            AddAbility(new WarcraftAbility("holy_shield", "Holy Shield",
                i => $"{ChatColors.BlueGrey}Surround yourself with a {ChatColors.Blue}protective barrier{ChatColors.BlueGrey} that absorbs incoming damage.")); //could be reworked to negate some damage instead (fits more with smite&undead)

            AddAbility(new WarcraftAbility("smite", "Smite",
                i => $"{ChatColors.BlueGrey}Infuse your attacks with {ChatColors.Yellow}divine{ChatColors.BlueGrey} energy, potentially stripping enemy armor."));

            AddAbility(new WarcraftAbility("divine_resurrection", "Divine Resurrection",
                i => $"{ChatColors.BlueGrey}Instantly {ChatColors.Red}revive{ChatColors.BlueGrey} a random fallen ally."));

            HookEvent<EventPlayerSpawn>("player_spawn", PlayerSpawn);
            HookEvent<EventPlayerDeath>("player_death", PlayerDeath);
            HookEvent<EventPlayerHurt>("player_hurt_other", PlayerHurtOther);
            HookEvent<EventRoundStart>("round_start", RoundStart);

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
            _healingAuraTimer = WarcraftPlugin.Instance.AddTimer(5f, () =>
            {
                if (Player == null || !Player.PawnIsAlive || !Player.PlayerPawn.IsValid)
                {
                    _healingAuraTimer?.Kill();
                    return;
                }

                var auraSize = WarcraftPlayer.GetAbilityLevel(0) * 100;
                var healingZone = Geometry.CreateBoxAroundPoint(Player.PlayerPawn.Value.AbsOrigin, auraSize, auraSize, auraSize);
                //Geometry.DrawVertices(healingZone.ComputeVertices()); //debug
                //Find players within area
                var playersToHeal = Utilities.GetPlayers().Where(x => x.Team == Player.Team && x.PawnIsAlive && Player.IsValid &&
                healingZone.Contains(x.PlayerPawn.Value.AbsOrigin.With().Add(z: 20).ToVector3d()));

                if (playersToHeal.Any())
                {
                    foreach (var player in playersToHeal)
                    {
                        if (player.PlayerPawn.Value.Health < player.PlayerPawn.Value.MaxHealth)
                        {
                            var healthAfterHeal = player.PlayerPawn.Value.Health + WarcraftPlayer.GetAbilityLevel(0);
                            player.SetHp(healthAfterHeal > player.PlayerPawn.Value.MaxHealth ? player.PlayerPawn.Value.MaxHealth : healthAfterHeal);
                            Utility.SpawnParticle(player.PlayerPawn.Value.AbsOrigin.With().Add(z: 40), "particles/ui/ammohealthcenter/ui_hud_kill_burn_fire.vpcf", 1);
                        }
                    }
                }
            }, TimerFlags.REPEAT);
        }

        private void PlayerDeath(EventPlayerDeath death)
        {
            _healingAuraTimer?.Kill();
        }

        public override void PlayerChangingToAnotherRace()
        {
            _healingAuraTimer?.Kill();
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
            if (!victim.IsValid || !victim.PawnIsAlive || victim.UserId == Player.UserId) return;

            double rolledValue = Random.Shared.NextDouble();
            float chanceToSmite = WarcraftPlayer.GetAbilityLevel(2) * 0.15f;

            if (rolledValue <= chanceToSmite)
            {
                if (victim.PlayerPawn.Value.ArmorValue > 0)
                {
                    victim.SetArmor(victim.PlayerPawn.Value.ArmorValue - WarcraftPlayer.GetAbilityLevel(2) * 5);
                    Utility.SpawnParticle(victim.PlayerPawn.Value.AbsOrigin.With().Add(z: 40), "particles/survival_fx/gas_cannister_impact_child_flash.vpcf", 1);
                    Player.PlayLocalSound("sounds/weapons/taser/taser_hit.vsnd");
                }
            }
        }
    }
}