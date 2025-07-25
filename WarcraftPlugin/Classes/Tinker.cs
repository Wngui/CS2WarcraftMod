using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using g3;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using WarcraftPlugin.Summons;
using static CounterStrikeSharp.API.Core.Listeners;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace WarcraftPlugin.Classes
{
    internal class Tinker : WarcraftClass
    {
        private readonly List<Drone> _drones = [];
        private static readonly Vector _droneDefaultPosition = new(70, -70, 90);
        private static readonly int _droneUltimateAmount = 3;
        private Timer _ultimateTimer;
        private const int _ultimateTime = 20;
        private OnTick? _updateDronesListener;

        public override string DisplayName => "Tinker";
        public override Color DefaultColor => Color.Teal;

        public override List<string> PreloadResources =>
        [
            "models/props/de_dust/hr_dust/dust_soccerball/dust_soccer_ball001.vmdl",
            "models/anubis/structures/pillar02_base01.vmdl"
        ];

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Attack Drone", "Deploy a drone that attacks nearby enemies. Damage and rocket chance scale with level."),
            new WarcraftAbility("Spare Parts", "4/8/12/16/20% chance to not lose ammo when firing."),
            new WarcraftAbility("Spring Trap", "Deploy a trap which launches players into the air with 500/1000/1500/2000/2500 force."),
            new WarcraftCooldownAbility("Drone Swarm", "Summon a swarm of attack drones that damage all nearby enemies.", 50f)
        ];

        public override void Register()
        {
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventPlayerDeath>(PlayerDeath);
            HookEvent<EventWeaponFire>(PlayerShoot);
            HookEvent<EventDecoyStarted>(DecoyStart);
            HookEvent<EventRoundEnd>(RoundEnd);

            HookEvent<EventSpottedEnemy>(SpottedPlayer);

            HookAbility(3, Ultimate);
        }

        private void SpottedPlayer(EventSpottedEnemy spotEvent)
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
            if (Warcraft.RollAbilityCheck(WarcraftPlayer.GetAbilityLevel(1), 20))
            {
                var activeWeapon = Player.PlayerPawn.Value.WeaponServices?.ActiveWeapon.Value;
                if (activeWeapon != null && activeWeapon.IsValid)
                {
                    activeWeapon.Clip1++;
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
                decoy.AttributeManager.Item.CustomName = Localizer["tinker.ability.2"];
            }
        }

        private void ActivateDrones(int numberOfDrones)
        {
            DeactivateDrones();
            for (int i = 0; i < numberOfDrones; i++)
            {
                _drones.Add(new Drone(Player, _droneDefaultPosition.Clone()));
            }

            _updateDronesListener = new OnTick(UpdateDrones);
            WarcraftPlugin.Instance.RegisterListener(_updateDronesListener);
        }

        private void UpdateDrones()
        {
            if (!Player.IsAlive())
            {
                DeactivateDrones();
                return;
            }

            foreach (var drone in _drones)
            {
                drone.Update();
            }
        }

        private void PlayerDeath(EventPlayerDeath death)
        {
            DeactivateDrones();
        }

        private void RoundEnd(EventRoundEnd end)
        {
            DeactivateDrones();
        }

        public override void PlayerChangingToAnotherRace()
        {
            DeactivateDrones();
            base.PlayerChangingToAnotherRace();
        }

        private void DeactivateDrones()
        {
            if (_updateDronesListener != null)
            {
                WarcraftPlugin.Instance.RemoveListener(_updateDronesListener);
                _updateDronesListener = null;
            }

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
                Utilities.GetEntityFromIndex<CDecoyProjectile>(decoy.Entityid)?.RemoveIfValid();
                new SpringTrapEffect(Player, 120, new Vector(decoy.X, decoy.Y, decoy.Z)).Start();
            }
        }

        internal class SpringTrapEffect(CCSPlayerController owner, float duration, Vector trapPosition) : WarcraftEffect(owner, duration)
        {
            private CDynamicProp _trap;
            private Box3d _triggerZone;

            private bool IsTriggered { get; set; } = false;

            public override void OnStart()
            {
                //trap model
                _trap = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
                _trap.Teleport(trapPosition.Clone().Add(z: -7), new QAngle(), new Vector());
                _trap.DispatchSpawn();
                _trap.SetModel("models/anubis/structures/pillar02_base01.vmdl");
                _trap.SetScale(0.5f);

                _triggerZone = Warcraft.CreateBoxAroundPoint(trapPosition, 100, 100, 100);
                //_triggerZone.Show(duration: Duration); //Debug
            }

            public override void OnTick()
            {
                if (!IsTriggered)
                {
                    //Find players in trap trigger zone
                    var players = Utilities.GetPlayers();
                    var playersInHurtZone = players.Where(x => x.PawnIsAlive && _triggerZone.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20))).ToList();
                    if (playersInHurtZone.Count != 0)
                    {
                        IsTriggered = true;
                        TriggerTrap(playersInHurtZone);
                    }
                }
            }

            private void TriggerTrap(List<CCSPlayerController> playersInTrap)
            {
                Warcraft.SpawnParticle(_trap.AbsOrigin.Clone().Add(z: 20), "particles/dev/materials_test_puffs.vpcf", 1);
                //Show trap
                _trap.SetColor(Color.FromArgb(255, 255, 255, 255));

                //launch players
                foreach (var player in playersInTrap)
                {
                    player.PlayerPawn.Value.AbsVelocity.Add(z: Owner.GetWarcraftPlayer().GetAbilityLevel(2) * 500);
                }

                _trap.EmitSound("security_door.open", volume: 0.7f);

                //Clean-up
                this.Destroy();
            }

            public override void OnFinish()
            {
                _trap?.RemoveIfValid();
            }
        }
        #endregion

        private void Ultimate()
        {
            //Ultimate effect
            var ultEffect = Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 40), "particles/ui/ui_experience_award_innerpoint.vpcf");
            ultEffect.SetParent(Player.PlayerPawn.Value);

            ActivateDrones(_droneUltimateAmount);

            // Define the offset for each drone's angle based on its index
            float angleOffsetPerDrone = (2 * (float)Math.PI) / _drones.Count;

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
            WarcraftPlugin.Instance.AddTimer(_ultimateTime, () =>
            {
                _ultimateTimer?.Kill();
                if (Player.IsAlive())
                    ActivateDrones(1);
            });
        }
    }
}