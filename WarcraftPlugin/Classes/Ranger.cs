using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API.Modules.Timers;
using System.Drawing;
using System.Linq;
using CounterStrikeSharp.API.Modules.Memory;
using g3;
using WarcraftPlugin.Models;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using WarcraftPlugin.Core.Effects;
using System.Collections.Generic;
using WarcraftPlugin.Events.ExtendedEvents;

namespace WarcraftPlugin.Classes
{
    internal class Ranger : WarcraftClass
    {
        private Timer _jumpTimer;
        private Timer _dashCooldownTimer;
        private bool _jumpedLastTick = false;
        private bool _dashOnCooldown = false;

        public override string DisplayName => "Ranger";
        public override Color DefaultColor => Color.Green;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Light footed", "Nimbly perform a dash in midair, by pressing jump"),
            new WarcraftAbility("Ensnare trap", "Place a trap by throwing a decoy"),
            new WarcraftAbility("Marksman", "Additional damage with scoped weapons"),
            new WarcraftCooldownAbility("Arrowstorm", "Call down a deadly volley of arrows using the ultimate key", 50f)
        ];

        public override void Register()
        {
            HookEvent<EventPlayerJump>(PlayerJump);
            HookEvent<EventDecoyStarted>(DecoyStart, HookMode.Post);
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookEvent<EventPlayerPing>(PlayerPing);
            HookEvent<EventPlayerSpawn>(PlayerSpawn);

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

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (!@event.Userid.IsAlive() || @event.Userid.UserId == Player.UserId) return;

            var markmansLevel = WarcraftPlayer.GetAbilityLevel(2);

            if (markmansLevel > 0 && WeaponTypes.Snipers.Contains(@event.Weapon))
            {
                var victim = @event.Userid;
                @event.AddBonusDamage(markmansLevel * 2);
                Warcraft.SpawnParticle(Player.CalculatePositionInFront(new Vector(10, 10, 60)), "particles/maps/de_overpass/chicken_impact_burst2.vpcf");
                Warcraft.SpawnParticle(victim.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 60), "particles/weapons/cs_weapon_fx/weapon_muzzle_flash_awp.vpcf");
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
                Utilities.GetEntityFromIndex<CDecoyProjectile>(decoy.Entityid)?.RemoveIfValid();
                WarcraftPlugin.Instance.AddTimer(1f, () =>
                {
                    new EnsnaringTrapEffect(Player, 20, new Vector(decoy.X, decoy.Y, decoy.Z)).Start();
                });
            }
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
            new ArrowStormEffect(Player, 10, new Vector(ping.X, ping.Y, ping.Z)).Start();
        }

        internal class EnsnaringTrapEffect(CCSPlayerController owner, float duration, Vector trapPosition) : WarcraftEffect(owner, duration)
        {
            private CPhysicsPropMultiplayer _trap;
            private CPhysicsPropMultiplayer _trigger;

            private Vector InitialPos { get; set; }
            private bool IsTriggered { get; set; } = false;

            public override void OnStart()
            {
                //Beartrap model
                _trap = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
                _trap.Teleport(trapPosition, new QAngle(), new Vector());
                _trap.DispatchSpawn();
                _trap.SetModel("models/weapons/w_eq_beartrap_dropped.vmdl");
                _trap.SetColor(Color.FromArgb(60, 255, 255, 255));

                //event prop
                _trigger = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
                _trigger.SetModel("models/props/de_dust/hr_dust/dust_crates/dust_crate_style_01_32x32x32.vmdl");
                _trigger.SetColor(Color.FromArgb(0, 255, 255, 255));
                _trigger.Teleport(trapPosition, new QAngle(), new Vector());
                _trigger.DispatchSpawn();

                if (_trigger.IsValid) InitialPos = _trigger?.AbsOrigin.Clone();
            }

            public override void OnTick()
            {
                if (!IsTriggered && _trigger.IsValid && !InitialPos.IsEqual(_trigger.AbsOrigin, true))
                {
                    IsTriggered = true;
                    _trigger?.RemoveIfValid();

                    TriggerTrap();
                }
            }

            private void TriggerTrap()
            {
                Warcraft.SpawnParticle(_trap.AbsOrigin.Clone().Add(z: 20), "particles/explosions_fx/explosion_hegrenade_water_ripple.vpcf", 1);
                //Show trap
                _trap.SetColor(Color.FromArgb(255, 255, 255, 255));
                //Create 3D box around trap
                var dangerzone = Warcraft.CreateBoxAroundPoint(_trap.AbsOrigin, 200, 200, 300);
                //Find players within area
                var players = Utilities.GetPlayers().Where(x => x.PlayerPawn.IsValid && x.PawnIsAlive);
                var playersInTrap = players.Where(x => dangerzone.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20)));
                //Set movement speed + small hurt
                if (playersInTrap.Any())
                {
                    foreach (var player in playersInTrap)
                    {
                        player.TakeDamage(Owner.GetWarcraftPlayer().GetAbilityLevel(1) * 10, Owner, KillFeedIcon.tripwirefire);
                        player.PlayerPawn.Value.VelocityModifier = 0;
                        player.PlayerPawn.Value.MovementServices.Maxspeed = 20;
                        Warcraft.SpawnParticle(player.CalculatePositionInFront(new Vector(10, 10, 60)), "particles/blood_impact/blood_impact_basic.vpcf");
                    }
                }
                //Clean-up
                WarcraftPlugin.Instance.AddTimer(3f, () =>
                {
                    foreach (var player in playersInTrap)
                    {
                        if (player.IsAlive())
                            player.PlayerPawn.Value.MovementServices.Maxspeed = 260;
                    }
                    this.Destroy();
                });
            }

            public override void OnFinish()
            {
                _trap?.RemoveIfValid();
                _trigger?.RemoveIfValid();
            }
        }

        internal class ArrowStormEffect(CCSPlayerController owner, float duration, Vector stormpos) : WarcraftEffect(owner, duration)
        {
            private readonly int _stormHeight = 150;
            private readonly int _stormArea = 280;
            private readonly int _arrowsPerVolley = 15;
            private Box3d _spawnBox;
            private Box3d _hurtBox;

            public override void OnStart()
            {
                var spawnBoxPoint = stormpos.With(z: stormpos.Z + _stormHeight);
                _spawnBox = Warcraft.CreateBoxAroundPoint(spawnBoxPoint, _stormArea, _stormArea, 50);

                var hurtBoxPoint = stormpos.With(z: stormpos.Z + _stormHeight / 2);
                _hurtBox = Warcraft.CreateBoxAroundPoint(hurtBoxPoint, _stormArea, _stormArea, _stormHeight);
                //_hurtBox.Show(duration: Duration); //Debug
                Owner.PlayLocalSound("sounds/music/damjanmravunac_01/deathcam.vsnd");
            }

            public override void OnTick()
            {
                Warcraft.SpawnParticle(_spawnBox.Center.ToVector().Add(z: 20), "particles/explosions_fx/explosion_hegrenade_water_ripple.vpcf", 1);

                HurtPlayersInside();

                for (int i = 0; i < _arrowsPerVolley; i++)
                {
                    SpawnArrow();
                }
            }

            private void HurtPlayersInside()
            {
                //Find players within area
                var players = Utilities.GetPlayers();
                var playersInHurtZone = players.Where(x => x.IsAlive() && _hurtBox.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20))).ToList();
                //Set movement speed + small hurt
                foreach (var player in playersInHurtZone)
                {
                    if (!player.IsAlive()) continue;

                    player.TakeDamage(2, Owner, KillFeedIcon.flair0);
                    player.PlayerPawn.Value.VelocityModifier = 0;
                    Warcraft.SpawnParticle(player.CalculatePositionInFront(new Vector(10, 10, 60)), "particles/blood_impact/blood_impact_basic.vpcf");
                }
            }

            private void SpawnArrow()
            {
                //Calculate new arrow pos
                var arrowSpawn = _spawnBox.GetRandomPoint();
                //Spawn arrow
                var arrow = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");
                if (!arrow.IsValid) return;
                arrow.Teleport(arrowSpawn, new QAngle(z: -90), new Vector());
                arrow.DispatchSpawn();
                arrow.SetModel("models/tools/bullet_hit_marker.vmdl");
                arrow.SetColor(Color.FromArgb(255, 45, 25, 25));
                arrow.SetScale(0.5f);

                arrow.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NEVER;
                arrow.Collision.SolidFlags = 12;
                arrow.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;

                Schema.SetSchemaValue(arrow.Handle, "CBaseGrenade", "m_hThrower", Owner.PlayerPawn.Raw); //Fixes killfeed

                //Cleanup
                WarcraftPlugin.Instance.AddTimer(0.6f, () => { arrow?.RemoveIfValid(); });
            }

            public override void OnFinish() { }
        }
    }
}