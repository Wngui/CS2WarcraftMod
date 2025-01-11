using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Events;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using WarcraftPlugin.Summons;
using static CounterStrikeSharp.API.Core.Listeners;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace WarcraftPlugin.Classes
{
    public class Tinker : WarcraftClass
    {
        private readonly List<Drone> _drones = [];
        private static readonly Vector _droneDefaultPosition = new(70, -70, 90);
        private static readonly int _droneUltimateAmount = 3;
        private Timer _ultimateTimer;
        private const int _ultimateTime = 20;

        public override string InternalName => "tinker";
        public override string DisplayName => "Tinker";
        public override DefaultClassModel DefaultModel => new()
        {
            //TModel = "characters/models/tm_phoenix/tm_phoenix_varianti.vmdl",
            //CTModel = "characters/models/ctm_swat/ctm_swat_variantj.vmdl"
        };
        public override Color DefaultColor => Color.Teal;

        public override void Register()
        {
            AddAbility(new WarcraftAbility("attack_drone", "Attack Drone",
                i => $"Deploy a gun drone that attacks nearby enemies."));

            AddAbility(new WarcraftAbility("spare_parts", "Spare Parts",
                i => $"Chance to not lose ammo when firing "));

            AddAbility(new WarcraftAbility("spring_trap", "Spring Trap",
                i => $"Deploy a trap which launches players into the air."));

            AddAbility(new WarcraftCooldownAbility("drone_swarm", "Drone Swarm",
                i => $"Summon a swarm of attack drones that damage all nearby enemies.",
                50f));

            HookEvent<EventPlayerSpawn>("player_spawn", PlayerSpawn);
            HookEvent<EventPlayerDeath>("player_death", PlayerDeath);
            HookEvent<EventWeaponFire>("player_shoot", PlayerShoot);
            HookEvent<EventDecoyStarted>("decoy_start", DecoyStart);

            HookEvent<EventSpottedPlayer>("spotted_enemy", SpottedPlayer);

            HookAbility(3, Ultimate);
        }

        private void SpottedPlayer(EventSpottedPlayer spotEvent)
        {
            if (_drones.Count != 0)
            {
                foreach (var drone in _drones)
                {
                    if (!drone.IsFireRateCooldown) drone.EnemySpotted(spotEvent.UserId);
                }
            }
        }

        private void PlayerShoot(EventWeaponFire fire)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                double rolledValue = Random.Shared.NextDouble();
                float sparePartsChance = WarcraftPlayer.GetAbilityLevel(0) * 0.02f;

                if (rolledValue <= sparePartsChance)
                {
                    var activeWeapon = Player.PlayerPawn.Value.WeaponServices?.ActiveWeapon.Value;
                    if (activeWeapon != null && activeWeapon.IsValid)
                    {
                        activeWeapon.Clip1++;
                    }
                }
            }
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            if (WarcraftPlayer.GetAbilityLevel(0) > 0)
            {
                ActivateDrones(1);
            }

            if (WarcraftPlayer.GetAbilityLevel(2) > 0)
            {
                var decoy = new CDecoyGrenade(Player.GiveNamedItem("weapon_decoy"));
                decoy.AttributeManager.Item.CustomName = "Spring Trap";
            }
        }

        private void ActivateDrones(int numberOfDrones)
        {
            DeactivateDrones();
            for (int i = 0; i < numberOfDrones; i++)
            {
                _drones.Add(new Drone(Player, _droneDefaultPosition.With()));
            }

            WarcraftPlugin.Instance.RegisterListener<OnTick>(UpdateDrones);
        }

        private void UpdateDrones()
        {
            foreach (var drone in _drones)
            {
                drone.Update();
            }
        }

        private void PlayerDeath(EventPlayerDeath death)
        {
            DeactivateDrones();
        }

        public override void PlayerChangingToAnotherRace()
        {
            DeactivateDrones();
        }

        private void DeactivateDrones()
        {
            var onTick = new OnTick(UpdateDrones);
            WarcraftPlugin.Instance.RemoveListener(onTick);

            foreach (var drone in _drones)
            {
                drone.Deactivate();
            }

            _drones.Clear();
        }

        #region Trap
        private void DecoyStart(EventDecoyStarted decoy)
        {
            if (WarcraftPlayer.GetAbilityLevel(2) > 0)
            {
                Utilities.GetEntityFromIndex<CDecoyProjectile>(decoy.Entityid)?.Remove();
                SpawnTrap(new Vector(decoy.X, decoy.Y, decoy.Z));
            }
        }

        private void SpawnTrap(Vector vector)
        {
            //trap model
            var trap = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            trap.Teleport(vector.With().Add(z: -7), new QAngle(), new Vector());
            trap.DispatchSpawn();
            trap.SetModel("models/anubis/structures/pillar02_base01.vmdl");
            trap.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 0.5f;

            //event prop
            var trigger = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
            trigger.SetModel("models/props/de_dust/hr_dust/dust_crates/dust_crate_style_01_32x32x32.vmdl");
            trigger.SetColor(Color.FromArgb(0, 255, 255, 255));
            trigger.Teleport(vector, new QAngle(), new Vector());
            trigger.DispatchSpawn();

            WarcraftPlugin.Instance.AddTimer(1f, () =>
            {
                DispatchEffect(new SpringTrapEffect(Player, trap, trigger, 120));
            });
        }

        public class SpringTrapEffect : WarcraftEffect
        {
            private readonly CDynamicProp _trap;
            private readonly CPhysicsPropMultiplayer _trigger;

            private Vector InitialPos { get; set; }
            private bool IsTriggered { get; set; } = false;

            public SpringTrapEffect(CCSPlayerController owner, CDynamicProp trap, CPhysicsPropMultiplayer trigger, float duration)
                : base(owner, duration)
            {
                _trap = trap;
                _trigger = trigger;
            }

            public override void OnStart()
            {
                InitialPos = _trigger?.AbsOrigin.With();
            }

            public override void OnTick()
            {
                if (!IsTriggered && _trigger.IsValid && !InitialPos.IsEqual(_trigger.AbsOrigin, true))
                {
                    IsTriggered = true;
                    _trigger?.Remove();

                    TriggerTrap();
                }
            }

            private void TriggerTrap()
            {
                Utility.SpawnParticle(_trap.AbsOrigin.With().Add(z: 20), "particles/dev/materials_test_puffs.vpcf", 1);
                //Show trap
                _trap.SetColor(Color.FromArgb(255, 255, 255, 255));

                //Create 3D box around trap
                var dangerzone = Geometry.CreateBoxAroundPoint(_trap.AbsOrigin, 200, 200, 300);
                //Find players within area
                var players = Utilities.GetPlayers();
                var playersInTrap = players.Where(x => dangerzone.Contains(x.PlayerPawn.Value.AbsOrigin.With().Add(z: 20).ToVector3d()));
                //launch players
                if (playersInTrap.Any())
                {
                    foreach (var player in playersInTrap)
                    {
                        player.PlayerPawn.Value.AbsVelocity.Add(z: Owner.GetWarcraftPlayer().GetAbilityLevel(2) * 500);
                        player.PlayLocalSound("sounds/buttons/lever6.vsnd");
                    }
                }

                //Clean-up
                _trap?.RemoveIfValid();
            }

            public override void OnFinish()
            {
                if (!IsTriggered)
                {
                    _trap?.RemoveIfValid();
                    _trigger?.RemoveIfValid();
                }
            }
        }
        #endregion

        private void Ultimate()
        {
            //Ultimate effect
            var ultEffect = Utility.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.With().Add(z:40), "particles/ui/ui_experience_award_innerpoint.vpcf");
            ultEffect.SetParent(Player.PlayerPawn.Value);

            ActivateDrones(_droneUltimateAmount);

            // Define the offset for each drone's angle based on its index
            float angleOffsetPerDrone = (2*(float)Math.PI) / _drones.Count;

            // Initialize the starting angle for each drone
            for (int i = 0; i < _drones.Count; i++)
            {
                _drones[i].Angle = i * angleOffsetPerDrone; // Give each drone a different starting angle
            }

            // Define the radius of the circle and the speed of rotation
            float radius = 80f; // Radius of the circular path
            float speed = 16f; // Speed of the circular motion

            // Create a timer to update the drone's position every 0.01 seconds
            _ultimateTimer = WarcraftPlugin.Instance.AddTimer(0.01f, () =>
            {
                foreach (var drone in _drones)
                {
                    // Update the X and Y positions using circular motion
                    drone.Position.X = radius * (float)Math.Cos(drone.Angle);
                    drone.Position.Y = radius * (float)Math.Sin(drone.Angle);

                    // Increase the angle over time to simulate movement along the circle
                    drone.Angle += speed * 0.01f; // Multiply by the timer's interval (0.01s)

                    // Keep the angle in the range of 0 to 2 * PI
                    if (drone.Angle > 2 * Math.PI)
                    {
                        drone.Angle -= 2 * (float)Math.PI;
                    }
                }
            }, TimerFlags.REPEAT);

            // End ultimate
            WarcraftPlugin.Instance.AddTimer(_ultimateTime, () => {
                _ultimateTimer?.Kill();
                ActivateDrones(1);
            });
        }
    }
}