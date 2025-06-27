using System;
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using CounterStrikeSharp.API;
using System.Collections.Generic;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Core.Effects;

namespace WarcraftPlugin.Classes
{
    internal class CryptLord : WarcraftClass
    {
        public override string DisplayName => "Crypt Lord";
        public override Color DefaultColor => Color.SaddleBrown;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Impale", "11/13/15/17/19% chance to launch enemies upward when attacking."),
            new WarcraftAbility("Spiked Carapace", "33% chance to reflect 25/30/35/40/45% of weapon damage."),
            new WarcraftAbility("Carrion Beetles", "50% chance to deal 3/4/5/6/7 bonus damage on attack."),
            new WarcraftCooldownAbility("Locust Swarm", "Drain 10% of nearby enemy health each second for 5s", 30f)
        ];

        public override void Register()
        {
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookEvent<EventPlayerHurt>(PlayerHurt);

            HookAbility(3, Ultimate);
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (!@event.Userid.IsAlive() || @event.Userid.UserId == Player.UserId) return;

            var impaleLevel = WarcraftPlayer.GetAbilityLevel(0);
            if (impaleLevel > 0)
            {
                var chance = 9 + impaleLevel * 2; //11-25%
                if (Random.Shared.Next(100) < chance)
                {
                    @event.Userid.PlayerPawn.Value.AbsVelocity.Add(z: 275);
                    Warcraft.SpawnParticle(@event.Userid.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 10), "particles/critters/chicken/chicken_impact_burst_zombie.vpcf", 1);
                    Player.PrintToChat($" {ChatColors.Green}Impaled {ChatColors.Default}{@event.Userid.GetRealPlayerName()}!");
                    @event.Userid.PrintToChat($" {ChatColors.Red}Impaled by {ChatColors.Default}{Player.GetRealPlayerName()}!");
                }
            }

            var beetleLevel = WarcraftPlayer.GetAbilityLevel(2);
            if (beetleLevel > 0)
            {
                if (Random.Shared.Next(100) < 50)
                {
                    var bonus = beetleLevel + 2;
                    @event.AddBonusDamage(bonus, abilityName: GetAbility(2).DisplayName);
                }
            }
        }

        private void PlayerHurt(EventPlayerHurt @event)
        {
            if (@event.Attacker == null || @event.Attacker.UserId == Player.UserId) return;

            var carapaceLevel = WarcraftPlayer.GetAbilityLevel(1);
            if (carapaceLevel > 0 && Random.Shared.Next(100) < 33)
            {
                var returnPct = 0.25f + 0.05f * (carapaceLevel - 1); //25%-60%
                var damage = @event.DmgHealth * returnPct;
                @event.Attacker.TakeDamage(damage, Player, KillFeedIcon.hammer);
                Player.PrintToChat($" {ChatColors.Green}Spiked Carapace reflected {ChatColors.LightYellow}{(int)damage}{ChatColors.Green} damage!");
                @event.Attacker.PrintToChat($" {ChatColors.Red}Spiked Carapace reflected {ChatColors.LightYellow}{(int)damage}{ChatColors.Red} damage back to you!");
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            new LocustSwarmEffect(Player, 5f).Start();
            StartCooldown(3);
        }

        internal class LocustSwarmEffect(CCSPlayerController owner, float duration) : WarcraftEffect(owner, duration, onTickInterval: 1)
        {
            public override void OnStart()
            {
                Owner.EmitSound("Player.DamageHelmet", volume: 0.5f);
            }

            public override void OnTick()
            {
                var level = Owner.GetWarcraftPlayer().GetAbilityLevel(3);
                var radius = 200 + 50 * level; //250-600
                var players = Utilities.GetPlayers();
                foreach (var p in players)
                {
                    if (!p.PawnIsAlive || p.Team == Owner.Team || p.UserId == Owner.UserId) continue;

                    var distance = (Owner.PlayerPawn.Value.AbsOrigin - p.PlayerPawn.Value.AbsOrigin).Length();
                    if (distance <= radius)
                    {
                        var drain = p.PlayerPawn.Value.MaxHealth * 0.10f;
                        p.TakeDamage(drain, Owner, KillFeedIcon.inferno);
                        if (Owner.PlayerPawn.Value.Health < Owner.PlayerPawn.Value.MaxHealth)
                        {
                            var heal = Math.Min(drain, Owner.PlayerPawn.Value.MaxHealth - Owner.PlayerPawn.Value.Health);
                            Owner.SetHp((int)(Owner.PlayerPawn.Value.Health + heal));
                        }
                    }
                }
            }

            public override void OnFinish() {}
        }
    }
}
