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
            new WarcraftAbility("Impale", "15/21/27/33/39% chance to launch enemies upward when attacking."),
            new WarcraftAbility("Spiked Carapace", "33% chance to reflect 25/30/35/40/45% of weapon damage."),
            new WarcraftAbility("Carrion Beetles", "50% chance to deal 1-3/1-4/1-5/1-6/1-7 bonus damage on attack."),
            new WarcraftCooldownAbility("Locust Swarm", "Deal 5 damage every 0.5s to nearby enemies for 3s, healing you for that amount.", 30f)
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
                var chance = 9 + impaleLevel * 6; //15-39%
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
                    var bonus = Random.Shared.Next(1, beetleLevel + 3);
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

            new LocustSwarmEffect(Player, 3f).Start();
            StartCooldown(3);
        }

        internal class LocustSwarmEffect(CCSPlayerController owner, float duration) : WarcraftEffect(owner, duration, onTickInterval: 0.5f)
        {
            public override void OnStart()
            {
                Owner.EmitSound("Player.DamageHelmet", volume: 0.5f);
                Owner.PrintToChat($" {ChatColors.Green}{Owner.GetWarcraftPlayer().GetClass().GetAbility(3).DisplayName}{ChatColors.Default} activated!");
            }

            public override void OnTick()
            {
                var level = Owner.GetWarcraftPlayer().GetAbilityLevel(3);
                var radius = 200 + 50 * level + 475; //425-775
                var players = Utilities.GetPlayers();
                foreach (var p in players)
                {
                    if (!p.PawnIsAlive || p.Team == Owner.Team || p.UserId == Owner.UserId) continue;

                    var distance = (Owner.PlayerPawn.Value.AbsOrigin - p.PlayerPawn.Value.AbsOrigin).Length();
                    if (distance <= radius)
                    {
                        var drain = 5f;
                        p.TakeDamage(drain, Owner, KillFeedIcon.inferno);
                        Owner.PrintToChat($" {ChatColors.Green}{Owner.GetWarcraftPlayer().GetClass().GetAbility(3).DisplayName}{ChatColors.Default} drained {(int)drain} HP from {p.GetRealPlayerName()}");
                        p.PrintToChat($" {ChatColors.Red}-{(int)drain} HP from {ChatColors.Green}{Owner.GetWarcraftPlayer().GetClass().GetAbility(3).DisplayName}");
                        if (Owner.PlayerPawn.Value.Health < Owner.PlayerPawn.Value.MaxHealth)
                        {
                            var newHealth = Math.Min(Owner.PlayerPawn.Value.Health + drain, Owner.PlayerPawn.Value.MaxHealth);
                            Owner.SetHp((int)newHealth);
                        }
                    }
                }
            }

            public override void OnFinish() {}
        }
    }
}
