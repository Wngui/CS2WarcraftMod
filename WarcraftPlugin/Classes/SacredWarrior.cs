using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Classes
{
    internal class SacredWarrior : WarcraftClass
    {
        public override string DisplayName => "Sacred Warrior";
        public override Color DefaultColor => Color.Orange;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Inner Vitality", "Passively recover 1/2/3/4/5 HP. When below 40% you heal twice as fast"),
            new WarcraftAbility("Burning Spear", "Lose 5% max HP, but set enemies ablaze. Deals 1/2/3/4/5 DPS for 3 seconds. Stacks 3 times"),
            new WarcraftAbility("Berserkers Blood", "Gain 1/2/3/4% move speed for each 7 percent of your health missing"),
            new WarcraftCooldownAbility("Life Break", "Damage yourself (20% of max HP) to deal a great amount of damage (40% of victim's max HP)", 40f)
        ];

        public override void Register()
        {
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookAbility(3, Ultimate);
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            if (WarcraftPlayer.GetAbilityLevel(0) > 0)
            {
                new InnerVitalityEffect(Player, WarcraftPlayer.GetAbilityLevel(0)).Start();
            }

            if (WarcraftPlayer.GetAbilityLevel(2) > 0)
            {
                new BerserkersBloodEffect(Player, WarcraftPlayer.GetAbilityLevel(2)).Start();
            }
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (!@event.Userid.IsAlive() || @event.Userid.AllyOf(Player)) return;

            var level = WarcraftPlayer.GetAbilityLevel(1);
            if (level > 0)
            {
                var origin = @event.Userid.PlayerPawn.Value.AbsOrigin;
                var radius = 75f;
                var enemies = Utilities.GetPlayers()
                    .Where(p => p.PawnIsAlive && !p.AllyOf(Player))
                    .Where(p => (p.PlayerPawn.Value.AbsOrigin - origin).Length() <= radius)
                    .ToList();

                foreach (var enemy in enemies)
                {
                    var effects = WarcraftPlugin.Instance.EffectManager.GetEffectsByType<BurningSpearEffect>()
                        .Where(x => x.Victim.Handle == enemy.Handle && x.Owner.Handle == Player.Handle)
                        .ToList();

                    if (effects.Count >= 3)
                    {
                        var oldest = effects.OrderBy(e => e.RemainingDuration).First();
                        oldest.Destroy();
                    }

                    new BurningSpearEffect(Player, enemy, 3f, level).Start();
                }

                var burnCost = (int)(Player.PlayerPawn.Value.MaxHealth * 0.05f);
                Player.TakeDamage(burnCost, Player, KillFeedIcon.inferno);
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            var trace = Player.RayTrace();
            if (trace == null) return;

            var target = Utilities.GetPlayers()
                .Where(p => p.PawnIsAlive && !p.AllyOf(Player))
                .OrderBy(p => (p.PlayerPawn.Value.AbsOrigin - trace).Length())
                .FirstOrDefault(p => p.PlayerPawn.Value.CollisionBox().Contains(trace) ||
                                     (p.PlayerPawn.Value.AbsOrigin - trace).Length() <= 50);
            if (target == null) {
                Player.PrintToChat($" {Localizer["effect.no.target", GetAbility(3).DisplayName]}");
                return;
            } 

            var selfDamage = (int)(Player.PlayerPawn.Value.MaxHealth * 0.2f);
            var damage = (int)(target.PlayerPawn.Value.MaxHealth * 0.4f);

            Player.TakeDamage(selfDamage, Player, KillFeedIcon.inferno);
            target.TakeDamage(damage, Player, KillFeedIcon.inferno);

            Warcraft.DrawLaserBetween(Player.EyePosition(-10), target.EyePosition(-10), Color.DarkOrange);
            Warcraft.SpawnParticle(target.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_hegrenade.vpcf", 1);
            Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/inferno_fx/firework_crate_ground_low_02.vpcf", 1);
            StartCooldown(3);
        }
    }

    // Heal should occur every 3 seconds instead of every second
    internal class InnerVitalityEffect(CCSPlayerController owner, int abilityLevel) : WarcraftEffect(owner, onTickInterval:3f)
    {
        public override void OnStart() { }
        public override void OnTick()
        {
            if (!Owner.IsAlive()) return;
            var pawn = Owner.PlayerPawn.Value;
            int heal = abilityLevel;
            if (pawn.Health < pawn.MaxHealth * 0.4f) heal *= 2;
            if (pawn.Health < pawn.MaxHealth)
            {
                Owner.SetHp((int)System.Math.Min(pawn.Health + heal, pawn.MaxHealth));
            }
        }
        public override void OnFinish() { }
    }

    internal class BurningSpearEffect(CCSPlayerController owner, CCSPlayerController victim, float duration, int damage)
        : WarcraftEffect(owner, duration, destroyOnDeath: false, onTickInterval:1f)
    {
        public CCSPlayerController Victim = victim;
        private CParticleSystem _particle;

        public override void OnStart()
        {
            if (Victim.IsAlive())
            {
                Owner.PrintToChat($" {ChatColors.Green}{Owner.GetWarcraftPlayer().GetClass().GetAbility(1).DisplayName}{ChatColors.Default} burning {Victim.GetRealPlayerName()}");
                Victim.PrintToChat($" {ChatColors.Red}Burning from {ChatColors.Green}{Owner.PlayerName}");

                _particle = Warcraft.SpawnParticle(Victim.PlayerPawn.Value.AbsOrigin, "particles/burning_fx/barrel_burning_engine_fire_static.vpcf", Duration);
                _particle.SetParent(Victim.PlayerPawn.Value);
            }
        }
        public override void OnTick()
        {
            if (!Victim.IsAlive()) { Destroy(); return; }

            Victim.TakeDamage(damage, Owner, KillFeedIcon.inferno);

            var abilityName = Owner.GetWarcraftPlayer().GetClass().GetAbility(1).DisplayName;
            Owner.PrintToChat($" {ChatColors.Green}{abilityName}{ChatColors.Default} +{damage} dmg");
            Victim.PrintToChat($" {ChatColors.Red}+{damage} dmg from {ChatColors.Green}{abilityName}");
        }
        public override void OnFinish() { _particle.RemoveIfValid(); }
    }

    internal class BerserkersBloodEffect(CCSPlayerController owner, int level) : WarcraftEffect(owner, onTickInterval:0.5f)
    {
        private float _baseModifier = 1f;
        public override void OnStart()
        {
            if (Owner.IsAlive())
            {
                _baseModifier = Owner.PlayerPawn.Value.VelocityModifier;
            }
        }
        public override void OnTick()
        {
            if (!Owner.IsAlive()) return;
            var pawn = Owner.PlayerPawn.Value;
            float missing = (pawn.MaxHealth - pawn.Health) / (float)pawn.MaxHealth;
            float stacks = missing / 0.07f;
            float speed = _baseModifier * (1f + level * 0.01f * stacks);
            pawn.VelocityModifier = speed;
        }
        public override void OnFinish()
        {
            if (Owner.IsAlive())
            {
                Owner.PlayerPawn.Value.VelocityModifier = _baseModifier;
            }
        }
    }
}
