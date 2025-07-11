﻿using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API;
using System.Linq;
using WarcraftPlugin.Models;
using System.Drawing;
using System.Collections.Generic;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Core.Effects;

namespace WarcraftPlugin.Classes
{
    internal class Paladin : WarcraftClass
    {
        private bool _hasUsedDivineResurrection = false;

        public override string DisplayName => "Paladin";
        public override Color DefaultColor => Color.Yellow;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Healing Aura", "Heal allies within 200/300/400/500/600 units for 1/2/3/4/5 HP every few seconds."),
            new WarcraftAbility("Holy Shield", "Gain an additional 20/40/60/80/100 armor."),
            new WarcraftAbility("Smite", "15/30/45/60/75% chance to strip enemy armor for 5/10/15/20/25 points."),
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
                new HealingAuraEffect(Player, 5f).Start();
            }

            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                Player.GiveNamedItem("item_assaultsuit");
                var armorBonus = WarcraftPlayer.GetAbilityLevel(1) * 20;
                Player.SetArmor(100 + armorBonus);
                Player.PrintToChat($" {ChatColors.Blue}+{armorBonus} armor {ChatColors.Gold}[{GetAbility(1).DisplayName}]");
            }
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
                Player.PrintToChat($"{ChatColors.Red}Divine resurrection already used this round.{ChatColors.Default}");
            }
        }

        private void DivineResurrection()
        {
            var deadTeamPlayers = Utilities.GetPlayers().Where(x => x.Team == Player.Team && !x.PawnIsAlive && x.IsValid);

            // Check if there are any players on the same team
            if (deadTeamPlayers.Any())
            {
                _hasUsedDivineResurrection = true;
                // Generate a random index within the range of players
                int randomIndex = Random.Shared.Next(deadTeamPlayers.Count());

                // Get the random player
                var playerToRevive = deadTeamPlayers.ElementAt(randomIndex);

                //Revive
                playerToRevive.Respawn();
                playerToRevive.PlayerPawn.Value.Teleport(Player.CalculatePositionInFront(10, 60), Player.PlayerPawn.Value.EyeAngles, new Vector());

                playerToRevive.PrintToChat(" " + Localizer["paladin.revive"]);
                Utilities.GetPlayers().ForEach(x =>
                    x.PrintToChat(" " + Localizer["paladin.revive.other", playerToRevive.GetRealPlayerName(), Player.GetRealPlayerName()]));
            }
            else
            {
                Player.PrintToChat(" " + Localizer["paladin.revive.none"]);
            }
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            var victim = @event.Userid;
            if (!victim.IsAlive() || victim.UserId == Player.UserId) return;

            //Smite
            if (victim.PlayerPawn.Value.ArmorValue > 0 && Warcraft.RollAbilityCheck(WarcraftPlayer.GetAbilityLevel(2), 75))
            {
                @event.AddBonusDamage(0, WarcraftPlayer.GetAbilityLevel(2) * 5, abilityName: GetAbility(2).DisplayName);
                Warcraft.SpawnParticle(victim.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 40), "particles/survival_fx/gas_cannister_impact_child_flash.vpcf", 1);
                victim.EmitSound("Weapon_Taser.Hit", volume: 0.1f);
            }
        }

        internal class HealingAuraEffect(CCSPlayerController owner, float onTickInterval) : WarcraftEffect(owner, onTickInterval: onTickInterval)
        {
            public override void OnStart() {}
            public override void OnTick()
            {
                var currentAbilityLevel = Owner.GetWarcraftPlayer().GetAbilityLevel(0);
                var auraSize = currentAbilityLevel * 100 + 100;
                var healingZone = Warcraft.CreateBoxAroundPoint(Owner.PlayerPawn.Value.AbsOrigin, auraSize, auraSize, auraSize);
                //healingZone.Show(duration: 2); //Debug
                //Find players within area
                var playersToHeal = Utilities.GetPlayers().Where(x => x.AllyOf(Owner) && x.PawnIsAlive && Owner.IsValid &&
                healingZone.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20)));

                if (playersToHeal.Any())
                {
                    foreach (var player in playersToHeal)
                    {
                        if (player.PlayerPawn.Value.Health < player.PlayerPawn.Value.MaxHealth)
                        {
                            player.Heal(currentAbilityLevel, healer: Owner);
                            Warcraft.SpawnParticle(player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 40), "particles/ui/ammohealthcenter/ui_hud_kill_burn_fire.vpcf", 1);
                        }
                    }
                }
            }
            public override void OnFinish(){}
        }
    }
}