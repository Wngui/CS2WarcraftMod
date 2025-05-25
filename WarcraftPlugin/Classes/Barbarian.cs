using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CounterStrikeSharp.API.Modules.Memory;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using System.Drawing;
using WarcraftPlugin.Core.Effects;
using System.Collections.Generic;
using WarcraftPlugin.Events.ExtendedEvents;

namespace WarcraftPlugin.Classes
{
    internal class Barbarian : WarcraftClass
    {
        public override string DisplayName => "Barbarian";
        public override DefaultClassModel DefaultModel => new()
        {
            TModel = "characters/models/tm_phoenix_heavy/tm_phoenix_heavy.vmdl",
            CTModel = "characters/models/ctm_heavy/ctm_heavy.vmdl"
        };

        public override List<string> PreloadResources =>
        [
            "models/cs_italy/props/barrel/italy_barrel_wood_1.vmdl"
        ];

        public override Color DefaultColor => Color.Brown;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Carnage", "Increase damage dealt with shotguns."),
            new WarcraftAbility("Battle-Hardened", "Increase your health by 20/40/60/80/100."),
            new WarcraftAbility("Throwing Axe", "Chance to throw an exploding barrel when firing."),
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
            var activeWeapon = Player.PlayerPawn.Value.WeaponServices?.ActiveWeapon.Value;
            if (activeWeapon != null && activeWeapon.IsValid)
            {
                var maxClip = activeWeapon.VData.MaxClip1;
                if (maxClip == 0) return;

                var maxChance = 400 / maxClip; // The bigger the mag, the lower the chance, to avoid negev spam

                if (Warcraft.RollDice(WarcraftPlayer.GetAbilityLevel(2), maxChance))
                {
                    new ThrowingAxeEffect(Player, 2).Start();
                }
            }
        }

        private void PlayerSpawn(EventPlayerSpawn @event)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                Server.NextFrame(() =>
                {
                    Player.SetHp(100 + WarcraftPlayer.GetAbilityLevel(1) * _battleHardenedHealthMultiplier);
                    Player.PlayerPawn.Value.MaxHealth = Player.PlayerPawn.Value.Health;
                });
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            new BloodlustEffect(Player, _bloodlustLength).Start();
            StartCooldown(3);
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (!@event.Userid.IsAlive() || @event.Userid.UserId == Player.UserId) return;

            var carnageLevel = WarcraftPlayer.GetAbilityLevel(0);

            if (carnageLevel > 0 && WeaponTypes.Shotguns.Contains(@event.Weapon))
            {
                var victim = @event.Userid;
                @event.AddBonusDamage(carnageLevel * 5);
                Warcraft.SpawnParticle(victim.PlayerPawn.Value.AbsOrigin.With(z: victim.PlayerPawn.Value.AbsOrigin.Z + 60), "particles/blood_impact/blood_impact_basic.vpcf");
                victim.EmitSound("Flesh.ImpactHard", volume:0.5f);
            }
        }
    }

    internal class ThrowingAxeEffect(CCSPlayerController owner, float duration) : WarcraftEffect(owner, duration)
    {
        private CHEGrenadeProjectile _throwingAxe;

        public override void OnStart()
        {
            _throwingAxe = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");

            Vector velocity = Owner.CalculateVelocityAwayFromPlayer(1800);

            var rotation = new QAngle(0, Owner.PlayerPawn.Value.EyeAngles.Y + 90, 0);

            _throwingAxe.Teleport(Owner.CalculatePositionInFront(10, 60), rotation, velocity);
            _throwingAxe.DispatchSpawn();
            _throwingAxe.SetModel("models/cs_italy/props/barrel/italy_barrel_wood_1.vmdl");
            Schema.SetSchemaValue(_throwingAxe.Handle, "CBaseGrenade", "m_hThrower", Owner.PlayerPawn.Raw); //Fixes killfeed

            _throwingAxe.AcceptInput("InitializeSpawnFromWorld");
            _throwingAxe.Damage = 40;
            _throwingAxe.DmgRadius = 180;
            _throwingAxe.DetonateTime = float.MaxValue;

            Owner.EmitSound("Door.wood_full_open", volume: 0.5f);
        }

        public override void OnTick()
        {
            if (!_throwingAxe.IsValid) return;
            var hasHitPlayer = _throwingAxe?.HasEverHitEnemy ?? false;
            if (hasHitPlayer)
            {
                try
                {
                    _throwingAxe.DetonateTime = 0;
                }
                catch { }
            }
        }

        public override void OnFinish()
        {
            if (_throwingAxe.IsValid)
            {
                _throwingAxe.DetonateTime = 0;
                WarcraftPlugin.Instance.AddTimer(1, () => _throwingAxe?.RemoveIfValid());
            }
        }
    }

    internal class BloodlustEffect(CCSPlayerController owner, float duration) : WarcraftEffect(owner, duration)
    {
        private const float _maxSize = 1.1f;

        public override void OnStart()
        {
            Owner.AdrenalineSurgeEffect(Duration);
            Owner.PlayerPawn.Value.VelocityModifier = 1.3f;
            Owner.PlayerPawn.Value.SetColor(Color.IndianRed);
            Owner.EmitSound("BaseGrenade.JumpThrowM", volume: 0.5f);
        }

        public override void OnTick()
        {
            if (!Owner.IsAlive()) return;

            //Refill ammo
            Owner.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.Clip1 = Owner.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.GetVData<CBasePlayerWeaponVData>().MaxClip1;

            //Regenerate health
            if (Owner.PlayerPawn.Value.Health < Owner.PlayerPawn.Value.MaxHealth)
            {
                Owner.SetHp(Owner.PlayerPawn.Value.Health + 1);
            }

            //Rage growth spurt
            var scale = Owner.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().Scale;
            if (scale < _maxSize)
            {
                Owner.PlayerPawn.Value.SetScale(scale + 0.01f);
            }
        }

        public override void OnFinish()
        {
            if (!Owner.IsAlive()) return;

            var pawn = Owner.PlayerPawn.Value;
            pawn.SetColor(Color.White);
            pawn.VelocityModifier = 1f;
            pawn.SetScale(1);
        }
    }
}