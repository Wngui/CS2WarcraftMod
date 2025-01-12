using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using System.Drawing;
using System;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace WarcraftPlugin.Summons
{
    public class Drone
    {
        private CPhysicsPropMultiplayer _drone;
        private CDynamicProp _model;
        private CDynamicProp _turret;
        public Vector Position { get; set; } = new(70, -70, 90);

        public bool IsFireRateCooldown { get; set; } = false;
        private readonly float _fireRate = 2f;
        private Timer _fireRateTimer;
        private CBeam _lazerDot;
        private Vector _target;
        private Timer _idleTimer;
        private readonly CCSPlayerController _owner;

        public float Angle { get; set; } = 0f;

        public Drone(CCSPlayerController owner, Vector position)
        {
            _owner = owner;
            Position = position;
            Activate();
        }

        private void Activate()
        {
            Deactivate();

            //Spawn animation
            var droneSpawnAnimation = Utility.SpawnParticle(_owner.CalculatePositionInFront(Position), "particles/ui/ui_electric_gold.vpcf");
            droneSpawnAnimation.SetParent(_owner.PlayerPawn.Value);

            //Create drone physics object
            _drone = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
            _drone.SetColor(Color.FromArgb(0, 255, 255, 255));
            _drone.SetModel("models/props/de_dust/hr_dust/dust_soccerball/dust_soccer_ball001.vmdl");
            _drone.DispatchSpawn();

            //Create drone body
            _model = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            _model.SetModel("models/weapons/w_eq_bumpmine.vmdl");
            _model.DispatchSpawn();

            //Create drone turret
            _turret = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            _turret.SetModel("models/tools/bullet_hit_marker.vmdl");
            _turret.DispatchSpawn();

            //Attach drone turret to body
            _turret.SetParent(_model, offset: new Vector(2, 2, 2), rotation: new QAngle(0, 310, 0));
            _turret.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 0.5f;
            _turret.SetColor(Color.FromArgb(255, 0, 0, 0));

            //Attach drone body to physics object
            _model.SetParent(_drone, rotation: new QAngle(175, 30, 0));
            _model.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 2;

            _drone.Teleport(_owner.CalculatePositionInFront(Position), _owner.PlayerPawn.Value.V_angle, new Vector(nint.Zero));
        }

        public void Deactivate()
        {
            _target = null;

            _turret?.RemoveIfValid();
            _model?.RemoveIfValid();
            _drone?.RemoveIfValid();
            _lazerDot?.RemoveIfValid();

            IsFireRateCooldown = false;
        }

        public void Update()
        {
            if (!_owner.IsValid || !_drone.IsValid) return;
            var nextDronePosition = _owner.CalculatePositionInFront(Position);
            Vector velocity = Utility.CalculateTravelVelocity(_drone.AbsOrigin, nextDronePosition, 0.5f);
            _drone.Teleport(null, _owner.PlayerPawn.Value.V_angle, velocity);

            //Ensure drone is not stuck
            float droneDistanceToPlayer = (_owner.PlayerPawn.Value.AbsOrigin - _drone.AbsOrigin).Length();
            if (droneDistanceToPlayer > 500) _drone.Teleport(_owner.CalculatePositionInFront(Position), _owner.PlayerPawn.Value.V_angle, new Vector(nint.Zero));

            //Update laser to point at target
            if (_target != null)
            {
                _lazerDot = Utility.DrawLaserBetween(_turret.CalculatePositionInFront(new Vector(0, 30, 2)), _target, Color.FromArgb(15, 255, 0, 0), 0.2f, 0.2f);
            }
        }

        public void EnemySpotted(CCSPlayerController enemy)
        {
            if (!IsFireRateCooldown)
            {
                var droneLevel = _owner.GetWarcraftPlayer().GetAbilityLevel(0);
                var timesToShoot = droneLevel + 3;

                TryShootTarget(enemy);

                for (var i = 0; i < timesToShoot; i++)
                {
                    double rolledValue = Random.Shared.NextDouble();
                    float rocketChance = droneLevel * 0.02f;

                    WarcraftPlugin.Instance.AddTimer((float)(0.2 * i), () => TryShootTarget(enemy, rolledValue <= rocketChance));
                }
            }
        }

        private void TryShootTarget(CCSPlayerController target, bool isRocket = false)
        {
            if (_turret != null && _turret.IsValid)
            {
                var playerCollison = target.PlayerPawn.Value.Collision.ToBox3d(target.PlayerPawn.Value.AbsOrigin);
                //Geometry.DrawVertices(playerCollison.ComputeVertices()); //debug

                //check if we have a clear line of sight to target
                var turretMuzzle = _turret.CalculatePositionInFront(new Vector(0, 30, 2));
                var endPos = RayTrace.TraceShape(turretMuzzle, playerCollison.Center.ToVector(), false);

                //ensure trace has hit the players hitbox
                if (endPos != null && playerCollison.Contains(endPos.ToVector3d()))
                {
                    _target = endPos;

                    if (!IsFireRateCooldown)
                    {
                        //start fireing cooldown
                        IsFireRateCooldown = true;
                        _fireRateTimer = WarcraftPlugin.Instance.AddTimer(_fireRate, () =>
                        {
                            IsFireRateCooldown = false; _target = null;
                        });
                    }

                    if (isRocket)
                    {
                        FireRocket(turretMuzzle, endPos);
                    }
                    else
                    {
                        Shoot(turretMuzzle, target);
                    }
                }
                else
                {
                    _target = null;
                }
            }
        }

        private void Shoot(Vector muzzle, CCSPlayerController target)
        {
            //particle effect from turret
            Utility.SpawnParticle(muzzle, "particles/weapons/cs_weapon_fx/weapon_muzzle_flash_assaultrifle.vpcf", 1);
            _turret.EmitSound("Weapon_M4A1.Silenced");

            //dodamage to target
            target.TakeDamage(_owner.GetWarcraftPlayer().GetAbilityLevel(0) * 1, _owner);
        }

        private void FireRocket(Vector muzzle, Vector endPos)
        {
            var rocket = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");

            Vector velocity = Utility.CalculateTravelVelocity(_turret.AbsOrigin, endPos, 1);

            rocket.Teleport(muzzle, rocket.AbsRotation, velocity);
            rocket.DispatchSpawn();
            Schema.SetSchemaValue(rocket.Handle, "CBaseGrenade", "m_hThrower", _owner.PlayerPawn.Raw); //Fixes killfeed

            //Rocket popping out the tube
            Utility.SpawnParticle(rocket.AbsOrigin, "particles/explosions_fx/explosion_hegrenade_smoketrails.vpcf", 1);
            rocket.EmitSound("Weapon_Nova.Pump");

            rocket.AcceptInput("InitializeSpawnFromWorld");

            rocket.Damage = 40;
            rocket.DmgRadius = 200;
        }
    }
}