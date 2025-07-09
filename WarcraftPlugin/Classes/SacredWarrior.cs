using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using WarcraftPlugin.Core.Effects;
using System.Drawing;
using WarcraftPlugin.Events.ExtendedEvents;
using System.Collections.Generic;
using System.Linq;

namespace WarcraftPlugin.Classes
{
    internal class SacredWarrior : WarcraftClass
    {
        public override string DisplayName => "Sacred Warrior";
        public override Color DefaultColor => Color.Orange;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Inner Vitality", "Passively recover 1/2/3/4/5 HP. When below 40% you heal twice as fast"),
            new WarcraftAbility("Burning Spear", "Passively lose 5% max HP, but set enemies ablaze. Deals 1/2/3/4/5 DPS for next 3 seconds. Stacks 3 times"),
            new WarcraftAbility("Berserkers Blood", "Gain 1/2/3/4 percent attack speed for each 7 percent of your health missing"),
            new WarcraftCooldownAbility("Life Break", "Damage yourself (20% of maxHP) to deal a great amount of damage (40% of victim's maxHP)", 40f)
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

            // Burning Spear no longer reduces max health on spawn; health cost
            // is applied each time the ability is used

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
                // Apply burning effect to the victim. Limit to 3 stacks per attacker
                var effects = WarcraftPlugin.Instance.EffectManager.GetEffectsByType<BurningSpearEffect>()
                    .Where(x => x.Victim.Handle == @event.Userid.Handle && x.Owner.Handle == Player.Handle)
                    .ToList();

                if (effects.Count >= 3)
                {
                    // Refresh the oldest stack
                    var oldest = effects.OrderBy(e => e.RemainingDuration).First();
                    oldest.Destroy();
                }

                new BurningSpearEffect(Player, @event.Userid, 3f, level).Start();

                // Each successful hit costs the warrior 5% of their maximum health
                var burnCost = (int)(Player.PlayerPawn.Value.MaxHealth * 0.05f);
                Player.TakeDamage(burnCost, Player, KillFeedIcon.inferno);
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            var trace = Player.RayTrace();
            if (trace == null) return;

            var target = Utilities.GetPlayers().FirstOrDefault(p => p.PawnIsAlive && !p.AllyOf(Player) && p.PlayerPawn.Value.CollisionBox().Contains(trace));
            if (target == null) return;

            var selfDamage = (int)(Player.PlayerPawn.Value.MaxHealth * 0.2f);
            var damage = (int)(target.PlayerPawn.Value.MaxHealth * 0.4f);

            Player.TakeDamage(selfDamage, Player, KillFeedIcon.inferno);
            target.TakeDamage(damage, Player, KillFeedIcon.inferno);
            Warcraft.SpawnParticle(target.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_hegrenade.vpcf", 2);
            StartCooldown(3);
        }
    }

    // Heal should occur every 3 seconds instead of every second
    internal class InnerVitalityEffect(CCSPlayerController owner, int abilityLevel) : WarcraftEffect(owner, duration:9999f, onTickInterval:3f)
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

    internal class BurningSpearEffect(CCSPlayerController owner, CCSPlayerController victim, float duration, int damage) : WarcraftEffect(owner, duration, onTickInterval:1f)
    {
        public CCSPlayerController Victim = victim;
        public override void OnStart()
        {
            if (Victim.IsAlive())
            {
                Warcraft.SpawnParticle(Victim.PlayerPawn.Value.AbsOrigin, "particles/inferno_fx/inferno_fire.vpcf", Duration);
                Owner.PrintToChat($" {ChatColors.Green}{Owner.GetWarcraftPlayer().GetClass().GetAbility(1).DisplayName}{ChatColors.Default} burning {Victim.GetRealPlayerName()}");
                Victim.PrintToChat($" {ChatColors.Red}Burning from {ChatColors.Green}{Owner.PlayerName}");
            }
        }
        public override void OnTick()
        {
            if (!Victim.IsAlive()) { Destroy(); return; }
            // Add burn damage as weapon damage so it stacks with other effects
            // Only show the burning kill icon if the victim dies from this tick
            var remaining = Victim.PlayerPawn.Value.Health - damage;
            KillFeedIcon? icon = remaining <= 0 ? KillFeedIcon.inferno : null;
            Victim.TakeDamage(damage, Owner, icon);

            var abilityName = Owner.GetWarcraftPlayer().GetClass().GetAbility(1).DisplayName;
            Owner.PrintToChat($" {ChatColors.Green}{abilityName}{ChatColors.Default} +{damage} dmg");
            Victim.PrintToChat($" {ChatColors.Red}+{damage} dmg from {ChatColors.Green}{abilityName}");
        }
        public override void OnFinish() { }
    }

    internal class BerserkersBloodEffect(CCSPlayerController owner, int level) : WarcraftEffect(owner, duration:9999f, onTickInterval:0.5f)
    {
        private float _baseModifier = 1f;
        public override void OnStart()
        {
            if (Owner.PlayerPawn?.Value != null)
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
            if (Owner.PlayerPawn?.Value != null)
            {
                Owner.PlayerPawn.Value.VelocityModifier = _baseModifier;
            }
        }
    }
}
