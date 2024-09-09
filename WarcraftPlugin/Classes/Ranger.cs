using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using WarcraftPlugin.Effects;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;
using System.Linq;
using CounterStrikeSharp.API.Modules.Memory;
using g3;
using WarcraftPlugin.Models;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace WarcraftPlugin.Classes
{
    public class Ranger : WarcraftClass
    {
        private Timer _jumpTimer;
        private Timer _dashCooldownTimer;
        private bool _jumpedLastTick = false;
        private bool _dashOnCooldown = false;

        public override string InternalName => "ranger";
        public override string DisplayName => "Ranger";
        public override DefaultClassModel DefaultModel => new()
        {
            //TModel = "",
            //CTModel = ""
        };
        public override Color DefaultColor => Color.Green;

        public override void Register()
        {
            AddAbility(new WarcraftAbility("light_footed", "Light footed",
                i => $"{ChatColors.BlueGrey}Nimbly perform a dash in midair, by pressing {ChatColors.Green}jump{ChatColors.Default}"));

            AddAbility(new WarcraftAbility("ensnare_trap", "Ensnare trap",
                i => $"{ChatColors.BlueGrey}Place a trap by throwing a {ChatColors.Blue}decoy{ChatColors.Default}"));

            AddAbility(new WarcraftAbility("marksman", "Marksman",
                i => $"{ChatColors.BlueGrey}Additional damage with {ChatColors.Yellow}scoped weapons{ChatColors.Default}"));

            AddAbility(new WarcraftCooldownAbility("arrowstorm", "Arrowstorm",
                i => $"{ChatColors.BlueGrey}Call down a {ChatColors.Red}deadly volley of arrows{ChatColors.BlueGrey} using the ultimate key",
                50f));

            HookEvent<EventPlayerJump>("player_jump", PlayerJump);
            HookEvent<EventDecoyStarted>("decoy_start", DecoyStart);
            HookEvent<EventPlayerHurt>("player_hurt_other", PlayerHurtOther);
            HookEvent<EventPlayerPing>("player_ping", PlayerPing);
            HookEvent<EventPlayerSpawn>("player_spawn", PlayerSpawn);

            HookAbility(3, Ultimate);
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                var decoy = new CDecoyGrenade(Player.GiveNamedItem("weapon_decoy"));
                decoy.AttributeManager.Item.CustomName = "Ensnare Trap";
            }
        }

        private void PlayerHurtOther(EventPlayerHurt @event)
        {
            if (!@event.Userid.IsValid || !@event.Userid.PawnIsAlive || @event.Userid.UserId == Player.UserId) return;

            var markmansLevel = WarcraftPlayer.GetAbilityLevel(2);

            if (markmansLevel > 0 && WeaponTypes.Snipers.Contains(@event.Weapon))
            {
                var victim = @event.Userid;
                victim.TakeDamage(markmansLevel * 2, Player);
                Utility.SpawnParticle(Player.CalculatePositionInFront(new Vector(10, 10, 60)), "particles/maps/de_overpass/chicken_impact_burst2.vpcf");
                Utility.SpawnParticle(victim.PlayerPawn.Value.AbsOrigin.With(z: victim.PlayerPawn.Value.AbsOrigin.Z + 60), "particles/weapons/cs_weapon_fx/weapon_muzzle_flash_awp.vpcf");
            }
        }

        #region Dash
        private void PlayerJump(EventPlayerJump @event)
        {
            if (WarcraftPlayer.GetAbilityLevel(0) > 0)
            {
                _jumpTimer?.Kill();
                _jumpedLastTick = true;
                _jumpTimer = WarcraftPlugin.Instance.AddTimer(0.1f, ExtraJump, TimerFlags.REPEAT);
            }
        }

        private void ExtraJump()
        {
            var isOnground = (Player.PlayerPawn.Value.Flags & (uint)PlayerFlags.FL_ONGROUND) == 1;

            if (isOnground)
            {
                _jumpTimer?.Kill();
                return; // Early exit if the player is on the ground
            }

            ulong buttonState = Player.PlayerPawn.Value.MovementServices.Buttons.ButtonStates[0];

            if (!_jumpedLastTick && (buttonState & (ulong)PlayerButtons.Jump) != 0)
            {
                Dash();
                _jumpTimer?.Kill();
            }
            else
            {
                _jumpedLastTick = false;
            }
        }

        private void Dash()
        {
            if (_dashOnCooldown)
            {
                Player.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Red}Dash{ChatColors.Default} on cooldown", 1);
                return; // Early exit if dash is on cooldown
            }

            BeginDashCooldown();

            ulong buttonState = Player.PlayerPawn.Value.MovementServices.Buttons.ButtonStates[0];
            var directionAngle = Player.PlayerPawn.Value.EyeAngles;

            directionAngle.Y +=
                        (buttonState & (ulong)PlayerButtons.Back) != 0 ? 180 :
                        (buttonState & (ulong)PlayerButtons.Moveleft) != 0 ? 90 :
                        (buttonState & (ulong)PlayerButtons.Moveright) != 0 ? -90 : 0;

            var directionVec = new Vector();
            NativeAPI.AngleVectors(directionAngle.Handle, directionVec.Handle, nint.Zero, nint.Zero);

            // Always shoot us up a little bit if were on the ground and not aiming up.
            if (directionVec.Z < 0.275)
            {
                directionVec.Z = 0.275f;
            }

            directionVec *= 700;

            Player.PlayerPawn.Value.AbsVelocity.X = directionVec.X;
            Player.PlayerPawn.Value.AbsVelocity.Y = directionVec.Y;
            Player.PlayerPawn.Value.AbsVelocity.Z = directionVec.Z;

            Player.PlayLocalSound("sounds/player/footsteps/jump_launch_01.vsnd");
        }

        private void BeginDashCooldown()
        {
            _dashOnCooldown = true;
            _dashCooldownTimer?.Kill();
            _dashCooldownTimer = WarcraftPlugin.Instance.AddTimer(10 / WarcraftPlayer.GetAbilityLevel(0), EndDashCooldown);
        }

        private void EndDashCooldown()
        {
            _dashOnCooldown = false;
            Player.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Green}Dash{ChatColors.Default} ready", 1);
        }
        #endregion
        #region Trap
        private void DecoyStart(EventDecoyStarted decoy)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                Utilities.GetEntityFromIndex<CDecoyProjectile>(decoy.Entityid)?.Remove();
                SpawnTrap(new Vector(decoy.X, decoy.Y, decoy.Z));
            }
        }

        private void SpawnTrap(Vector vector)
        {
            //Beartrap model
            var trap = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
            trap.Teleport(vector, new QAngle(), new Vector());
            trap.DispatchSpawn();
            trap.SetModel("models/weapons/w_eq_beartrap_dropped.vmdl");
            trap.SetColor(Color.FromArgb(60, 255, 255, 255));

            //event prop
            var trigger = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
            trigger.SetModel("models/props/de_dust/hr_dust/dust_crates/dust_crate_style_01_32x32x32.vmdl");
            trigger.SetColor(Color.FromArgb(0, 255, 255, 255));
            trigger.Teleport(vector, new QAngle(), new Vector());
            trigger.DispatchSpawn();

            WarcraftPlugin.Instance.AddTimer(1f, () =>
            {
                DispatchEffect(new EnsnaringTrapEffect(Player, trap, trigger, 120));
            });
        }
        #endregion

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            // Hack to get players aim point in the world, see player ping event
            Player.ExecuteClientCommandFromServer("player_ping");
        }

        private void PlayerPing(EventPlayerPing ping)
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;
            StartCooldown(3);
            DispatchEffect(new ArrowStormEffect(Player, new Vector(ping.X, ping.Y, ping.Z), 10));
        }

        public class EnsnaringTrapEffect : WarcraftEffect
        {
            private readonly CPhysicsPropMultiplayer _trap;
            private readonly CPhysicsPropMultiplayer _trigger;

            private Vector InitialPos { get; set; }
            private bool IsTriggered { get; set; } = false;

            public EnsnaringTrapEffect(CCSPlayerController owner, CPhysicsPropMultiplayer trap, CPhysicsPropMultiplayer trigger, float duration)
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
                if (!IsTriggered && !InitialPos.IsEqual(_trigger.AbsOrigin, true))
                {
                    IsTriggered = true;
                    _trigger?.Remove();

                    TriggerTrap();
                }
            }

            private void TriggerTrap()
            {
                Utility.SpawnParticle(_trap.AbsOrigin.With().Add(z: 20), "particles/explosions_fx/explosion_hegrenade_water_ripple.vpcf", 1);
                //Show trap
                _trap.SetColor(Color.FromArgb(255, 255, 255, 255));
                //Create 3D box around trap
                var dangerzone = Geometry.CreateBoxAroundPoint(_trap.AbsOrigin, 200, 200, 300);
                //Find players within area
                var players = Utilities.GetPlayers();
                var playersInTrap = players.Where(x => dangerzone.Contains(x.PlayerPawn.Value.AbsOrigin.With().Add(z: 20).ToVector3d()));
                //Set movement speed + small hurt
                if (playersInTrap.Any())
                {
                    foreach (var player in playersInTrap)
                    {
                        player.TakeDamage(Owner.GetWarcraftPlayer().GetAbilityLevel(1) * 10, Owner);
                        player.PlayerPawn.Value.VelocityModifier = 0;
                        player.PlayerPawn.Value.MovementServices.Maxspeed = 20;
                        Utility.SpawnParticle(player.CalculatePositionInFront(new Vector(10, 10, 60)), "particles/blood_impact/blood_impact_basic.vpcf");
                    }
                }
                //Clean-up
                Timer timer = null;
                timer = WarcraftPlugin.Instance.AddTimer(3f, () =>
                {
                    foreach (var player in playersInTrap)
                    {
                        player.PlayerPawn.Value.MovementServices.Maxspeed = 260;
                    }
                    _trap?.Remove();
                    timer.Kill();
                }, TimerFlags.REPEAT);
            }

            public override void OnFinish()
            {
                if (!IsTriggered)
                {
                    _trap?.Remove();
                    _trigger?.Remove();
                }
            }
        }

        public class ArrowStormEffect : WarcraftEffect
        {
            private readonly Box3d _spawnBox;
            private readonly Box3d _hurtBox;
            private readonly Random _random;

            private readonly int _stormHeight = 150;
            private readonly int _stormArea = 280;

            public ArrowStormEffect(CCSPlayerController owner, Vector stormpos, float duration)
            : base(owner, duration)
            {
                _random = Random.Shared;

                var spawnBoxPoint = stormpos.With(z: stormpos.Z + _stormHeight);
                _spawnBox = Geometry.CreateBoxAroundPoint(spawnBoxPoint, _stormArea, _stormArea, 50);

                var hurtBoxPoint = stormpos.With(z: stormpos.Z + _stormHeight / 2);
                _hurtBox = Geometry.CreateBoxAroundPoint(hurtBoxPoint, _stormArea, _stormArea, _stormHeight);

            }

            public override void OnStart()
            {
                //Geometry.DrawVertices(_hurtBox.ComputeVertices()); //debug
                Owner.PlayLocalSound("sounds/music/damjanmravunac_01/deathcam.vsnd");
            }

            public override void OnTick()
            {
                Utility.SpawnParticle(_spawnBox.Center.ToVector().Add(z: 20), "particles/explosions_fx/explosion_hegrenade_water_ripple.vpcf", 1);

                HurtPlayersInside();

                for (int i = 0; i < 15; i++)
                {
                    SpawnArrow();
                }
            }

            private void HurtPlayersInside()
            {
                //Find players within area
                var players = Utilities.GetPlayers();
                var playersInHurtZone = players.Where(x => _hurtBox.Contains(x.PlayerPawn.Value.AbsOrigin.With().Add(z: 20).ToVector3d()));
                //Set movement speed + small hurt
                if (playersInHurtZone.Any())
                {
                    foreach (var player in playersInHurtZone)
                    {
                        player.TakeDamage(1, Owner);
                        player.PlayerPawn.Value.VelocityModifier = 0;
                        Utility.SpawnParticle(player.CalculatePositionInFront(new Vector(10, 10, 60)), "particles/blood_impact/blood_impact_basic.vpcf");
                    }
                }
            }

            private void SpawnArrow()
            {
                //Calculate new arrow pos
                var arrowSpawn = _spawnBox.GetRandomPoint(_random);
                //Spawn arrow
                var arrow = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");
                if (!arrow.IsValid) return;
                arrow.Teleport(arrowSpawn, new QAngle(z: -90), new Vector());
                arrow.DispatchSpawn();
                arrow.SetModel("models/tools/bullet_hit_marker.vmdl");
                arrow.SetColor(Color.FromArgb(255, 45, 25, 25));
                arrow.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 0.5f;

                arrow.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NEVER;
                arrow.Collision.SolidFlags = 12;
                arrow.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;

                Schema.SetSchemaValue(arrow.Handle, "CBaseGrenade", "m_hThrower", Owner.PlayerPawn.Raw); //Fixes killfeed

                //Cleanup
                Timer timer = null;
                timer = WarcraftPlugin.Instance.AddTimer(0.6f, () =>
                {
                    arrow?.Remove();
                    timer.Kill();
                }, TimerFlags.REPEAT);
            }

            public override void OnFinish()
            {
            }
        }
    }
}