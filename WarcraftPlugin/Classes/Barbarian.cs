using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CounterStrikeSharp.API.Modules.Memory;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using System.Drawing;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Events;
using System.Collections.Generic;

namespace WarcraftPlugin.Classes
{
    public class Barbarian : WarcraftClass
    {
        public override string DisplayName => "Barbarian";
        public override DefaultClassModel DefaultModel => new()
        {
            TModel = "characters/models/tm_phoenix_heavy/tm_phoenix_heavy.vmdl",
            CTModel = "characters/models/ctm_heavy/ctm_heavy.vmdl"
        };
        public override Color DefaultColor => Color.Brown;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Carnage", "Increase damage dealt with shotguns."),
            new WarcraftAbility("Battle-Hardened", "Increase your health by 20/40/60/80/100."),
            new WarcraftAbility("Throwing Axe", "Chance to hurl an exploding throwing axe when firing."),
            new WarcraftCooldownAbility("Bloodlust", "Grants infinite ammo, movement speed & health regeneration.", 50f)
        ];

        private readonly int _battleHardenedHealthMultiplier = 20;
        private readonly float _bloodlustLength = 10;

        public override void Register()
        {
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventWeaponFire>(PlayerShoot);

            HookAbility(3, Ultimate);
        }

        private void PlayerShoot(EventWeaponFire @event)
        {
            if (WarcraftPlayer.GetAbilityLevel(2) > 0)
            {
                double rolledValue = Random.Shared.NextDouble();
                float chanceToAxe = WarcraftPlayer.GetAbilityLevel(2) * 0.05f;

                if (rolledValue <= chanceToAxe)
                {
                    ThrowAxe();
                }
            }
        }

        private void ThrowAxe()
        {
            var throwingAxe = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");

            Vector velocity = Player.CalculateVelocityAwayFromPlayer(1800);

            var rotation = new QAngle(0, Player.PlayerPawn.Value.EyeAngles.Y + 90, 0);

            throwingAxe.Teleport(Player.CalculatePositionInFront(new Vector(10, 10, 60)), rotation, velocity);
            throwingAxe.DispatchSpawn();
            throwingAxe.SetModel("models/weapons/v_axe.vmdl");
            Schema.SetSchemaValue(throwingAxe.Handle, "CBaseGrenade", "m_hThrower", Player.PlayerPawn.Raw); //Fixes killfeed

            throwingAxe.AcceptInput("InitializeSpawnFromWorld");
            throwingAxe.Damage = 40;
            throwingAxe.DmgRadius = 180;
            throwingAxe.DetonateTime = float.MaxValue;
            DispatchEffect(new ThrowingAxeEffect(Player, throwingAxe, 2));
        }

        private void PlayerSpawn(EventPlayerSpawn @event)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                Player.SetHp(100 + WarcraftPlayer.GetAbilityLevel(1) * _battleHardenedHealthMultiplier);
                Player.PlayerPawn.Value.MaxHealth = Player.PlayerPawn.Value.Health;
            }
        }

        private void SetBloodlust()
        {
            Player.PlayerPawn.Value.HealthShotBoostExpirationTime = Server.CurrentTime + _bloodlustLength;
            Utilities.SetStateChanged(Player.PlayerPawn.Value, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
            DispatchEffect(new BloodlustEffect(Player, _bloodlustLength));
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            SetBloodlust();
            StartCooldown(3);
        }

        private void PlayerHurtOther(EventPlayerHurt @event)
        {
            if (!@event.Userid.IsValid || !@event.Userid.PawnIsAlive || @event.Userid.UserId == Player.UserId) return;

            var carnageLevel = WarcraftPlayer.GetAbilityLevel(0);

            if (carnageLevel > 0 && WeaponTypes.Shotguns.Contains(@event.Weapon))
            {
                var victim = @event.Userid;
                victim.TakeDamage(carnageLevel * 5f, Player);
                Warcraft.SpawnParticle(victim.PlayerPawn.Value.AbsOrigin.With(z: victim.PlayerPawn.Value.AbsOrigin.Z + 60), "particles/blood_impact/blood_impact_basic.vpcf");
                Player.PlayLocalSound("sounds/physics/body/body_medium_break3.vsnd");
            }
        }
    }

    public class ThrowingAxeEffect : WarcraftEffect
    {
        private readonly CHEGrenadeProjectile _axe;

        public ThrowingAxeEffect(CCSPlayerController owner, CHEGrenadeProjectile axe, float duration) : base(owner, duration) { _axe = axe; }

        public override void OnStart()
        {
            Owner.PlayLocalSound("sounds/player/effort_m_09.vsnd");
        }

        public override void OnTick()
        {
            if (!_axe.IsValid) return;
            var hasHitPlayer = _axe?.HasEverHitPlayer ?? false;
            if (hasHitPlayer)
            {
                try
                {
                    _axe.DetonateTime = 0;
                }
                catch { }
            }
        }

        public override void OnFinish()
        {
            if (_axe.IsValid)
            {
                _axe.DetonateTime = 0;
                WarcraftPlugin.Instance.AddTimer(1, () => _axe?.RemoveIfValid());
            }
        }
    }

    public class BloodlustEffect : WarcraftEffect
    {
        public BloodlustEffect(CCSPlayerController owner, float duration) : base(owner, duration) { }

        private const float _maxSize = 1.1f;

        public override void OnStart()
        {
            Owner.PlayerPawn.Value.VelocityModifier = 1.3f;
            Owner.PlayerPawn.Value.SetColor(Color.IndianRed);
            Owner.PlayLocalSound("sounds/vo/agents/balkan/t_death03.vsnd");
        }

        public override void OnTick()
        {
            if (!Owner.IsValid || !Owner.PawnIsAlive) return;

            //Refill ammo
            Owner.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.Clip1 = Owner.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.GetVData<CBasePlayerWeaponVData>().MaxClip1;

            //Regenerate healthw
            if (Owner.PlayerPawn.Value.Health < Owner.PlayerPawn.Value.MaxHealth)
            {
                Owner.SetHp(Owner.PlayerPawn.Value.Health + 1);
            }

            //Rage growth spurt
            if (Owner.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().Scale < _maxSize)
            {
                Owner.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().Scale += 0.01f;
                Utilities.SetStateChanged(Owner.PlayerPawn.Value, "CBaseEntity", "m_CBodyComponent");
            }
        }

        public override void OnFinish()
        {
            if (!Owner.IsValid || !Owner.PawnIsAlive) return;

            Owner.PlayerPawn.Value.SetColor(Color.White);
            Owner.PlayerPawn.Value.VelocityModifier = 1f;
            Owner.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 1f;
            Utilities.SetStateChanged(Owner.PlayerPawn.Value, "CBaseEntity", "m_CBodyComponent");
        }
    }
}