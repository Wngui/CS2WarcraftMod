using CounterStrikeSharp.API.Core;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using System.Drawing;
using WarcraftPlugin.Core.Effects;
using System.Collections.Generic;
using WarcraftPlugin.Events.ExtendedEvents;
using System.Linq;
using System;

namespace WarcraftPlugin.Classes
{
    internal class Shadowblade : WarcraftClass
    {
        public override string DisplayName => "Shadowblade";
        public override DefaultClassModel DefaultModel => new()
        {
            TModel = "characters/models/ctm_st6/ctm_st6_variantn.vmdl",
            CTModel = "characters/models/ctm_st6/ctm_st6_variantn.vmdl"
        };

        public override Color DefaultColor => Color.Violet;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Shadowstep", "Chance to teleport behind enemy when taking damage."),
            new WarcraftAbility("Evasion", "Chance to completely dodge incoming damage."),
            new WarcraftAbility("Venom Strike", "Your attacks poison enemies, dealing damage over time."),
            new WarcraftCooldownAbility("Cloak of Shadows", "Become invisible for a short duration.", 40f)
        ];

        private const float _venomDuration = 4f;
        private readonly float _cloakDuration = 6f;

        public override void Register()
        {
            HookEvent<EventPlayerHurt>(PlayerHurt);
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookAbility(3, Ultimate);
        }

        private void PlayerHurt(EventPlayerHurt @event)
        {
            var attacker = @event.Attacker;
            // Evasion: Chance to dodge damage
            if (Warcraft.RollDice(WarcraftPlayer.GetAbilityLevel(1), 30))
            {
                Player.PrintToChat(" " + Localizer["shadowblade.evaded", @event.DmgHealth]);
                attacker.PrintToChat(" " + Localizer["shadowblade.evaded", Player.GetRealPlayerName()]);
                @event.IgnoreDamage();
                Player.PlayerPawn.Value.EmitSound("BulletBy.Subsonic", volume: 0.2f);
                var particle = Warcraft.SpawnParticle(Player.EyePosition(-50), "particles/explosions_fx/explosion_hegrenade_dirt_ground.vpcf");
                particle.SetParent(Player.PlayerPawn.Value);

                return;
            }

            // Shadowstep
            if (Warcraft.RollDice(WarcraftPlayer.GetAbilityLevel(1), 20))
            {
                var posBehindEnemy = attacker.CalculatePositionInFront(-90, attacker.EyeHeight());

                // Check that we have visibility of the enemy
                var enemyPos = Warcraft.RayTrace(posBehindEnemy, attacker.EyePosition());
                var enemyVisible = enemyPos != null && attacker.PlayerPawn.Value.CollisionBox().Contains(enemyPos);

                // Check that we have ground to stand on!
                var groundDistance = posBehindEnemy.With().Add(z: -500);
                var groundPos = Warcraft.RayTrace(posBehindEnemy, groundDistance);
                var aboveGround = groundPos != null && groundPos.Z > groundDistance.Z;

                if (enemyVisible & aboveGround)
                {
                    Player.PlayerPawn.Value.Teleport(posBehindEnemy, attacker.PlayerPawn.Value.EyeAngles, Vector.Zero);
                    Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/survival_fx/danger_zone_loop_black.vpcf", 2);
                    Player.PlayerPawn.Value.EmitSound("UI.PlayerPingUrgent", volume: 0.2f);
                    Player.PrintToChat(" " + Localizer["shadowblade.shadowstep"]);
                }
            }
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (@event.Userid.AllyOf(Player)) return;

            // Venom Strike: Poison on knife hit
            var venomLevel = WarcraftPlayer.GetAbilityLevel(2);
            if (venomLevel > 0)
            {
                var isVictimPoisoned = WarcraftPlugin.Instance.EffectManager.GetEffectsByType<VenomStrikeEffect>()
                    .Any(x => x.Victim.Handle == @event.Userid.Handle);
                if (!isVictimPoisoned)
                {
                    new VenomStrikeEffect(Player, @event.Userid, _venomDuration, venomLevel).Start();
                }
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            new InvisibleEffect(Player, _cloakDuration).Start();
            StartCooldown(3);
        }
    }

    internal class VenomStrikeEffect(CCSPlayerController owner, CCSPlayerController victim, float duration, int level) : WarcraftEffect(owner, duration, onTickInterval: 1f)
    {
        public CCSPlayerController Victim = victim;

        public override void OnStart()
        {
            if (!Victim.IsAlive()) return;
            Warcraft.SpawnParticle(Victim.EyePosition(-10), "particles/critters/chicken/chicken_impact_burst_zombie.vpcf", 1);
        }

        public override void OnTick()
        {
            if (!Victim.IsAlive()) return;
            Warcraft.SpawnParticle(Victim.EyePosition(-10), "particles/critters/chicken/chicken_impact_burst_zombie.vpcf", 1);
            Victim.PlayerPawn.Value.EmitSound("Player.DamageFall.Fem", volume: 0.2f);
            Victim.TakeDamage(level, Owner, KillFeedIcon.bayonet);
            Owner.PrintToChat(" " + Localizer["shadowblade.venomstrike.victim", level]);
            Owner.PrintToChat(" " + Localizer["shadowblade.venomstrike", Victim.GetRealPlayerName(), level]);
        }

        public override void OnFinish()
        {
        }
    }

    internal class InvisibleEffect(CCSPlayerController owner, float duration) : WarcraftEffect(owner, duration)
    {
        public override void OnStart()
        {
            if (!Owner.IsAlive()) return;
            Owner.PrintToCenter(Localizer["rogue.invsible"]);
            Owner.PlayerPawn.Value.SetColor(Color.FromArgb(0, 255, 255, 255));

            Owner.AdrenalineSurgeEffect(Duration);
            Owner.PlayerPawn.Value.VelocityModifier = 2f;
        }
        public override void OnTick() { }
        public override void OnFinish()
        {
            if (!Owner.IsAlive()) return;
            Owner.GetWarcraftPlayer().GetClass().SetDefaultAppearance();
            Owner.PrintToCenter(Localizer["rogue.visible"]);
            Owner.PlayerPawn.Value.VelocityModifier = 1f;
        }
    }
}
