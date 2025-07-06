using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using WarcraftPlugin.Helpers;
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
        private DashEffect _dashEffect;

        public override string DisplayName => "Ranger";
        public override Color DefaultColor => Color.Green;

        public override List<string> PreloadResources =>
        [
            "models/generic/street_trashcan_03/street_trashcan_03_lid_a.vmdl"
        ];

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftCooldownAbility("Light footed", "Nimbly perform a dash in midair, cooldown decreases from 10s to 2s as you level up.", () => 10 / WarcraftPlayer.GetAbilityLevel(0)),
            new WarcraftAbility("Ensnare trap", "Place a trap by throwing a decoy that deals 10/20/30/40/50 damage on trigger."),
            new WarcraftAbility("Marksman", "Deal 2/4/6/8/10 bonus damage with scoped weapons."),
            new WarcraftCooldownAbility("Arrowstorm", "Ping a point to rain arrows for 10s, hurting and slowing foes", 50f)
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
                decoy.AttributeManager.Item.CustomName = Localizer["ranger.ability.1"];
            }
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (!@event.Userid.IsAlive() || @event.Userid.UserId == Player.UserId) return;

            var markmansLevel = WarcraftPlayer.GetAbilityLevel(2);

            if (markmansLevel > 0 && WeaponTypes.Snipers.Contains(@event.Weapon))
            {
                var victim = @event.Userid;
                @event.AddBonusDamage(markmansLevel * 2, abilityName: GetAbility(2).DisplayName);
                Warcraft.SpawnParticle(victim.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 60), "particles/maps/de_overpass/chicken_impact_burst2.vpcf");
            }
        }

        #region Dash
        private void PlayerJump(EventPlayerJump @event)
        {
            if (WarcraftPlayer.GetAbilityLevel(0) > 0 && IsAbilityReady(0))
            {
                _dashEffect?.Destroy();
                _dashEffect = new DashEffect(Player);
                _dashEffect.Start();
            }
        }

        #endregion
        #region Trap
        private void DecoyStart(EventDecoyStarted decoy)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                Utilities.GetEntityFromIndex<CDecoyProjectile>(decoy.Entityid)?.RemoveIfValid();
                new EnsnaringTrapEffect(Player, 20, new Vector(decoy.X, decoy.Y, decoy.Z)).Start();
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

        internal class DashEffect(CCSPlayerController owner) : WarcraftEffect(owner, onTickInterval: 0.1f)
        {
            private readonly float _extraJumpDelay = 0.2f;
            private float _extraJumpDelayTick;

            public override void OnStart()
            {
                _extraJumpDelayTick = Server.CurrentTime + _extraJumpDelay;
            }

            public override void OnTick()
            {
                //Effect is destroyed if player is on the ground
                if (!Owner.IsAlive() || (Owner.PlayerPawn.Value.Flags & (uint)PlayerFlags.FL_ONGROUND) != 0)
                {
                    this.Destroy();
                    return;
                }

                ulong buttonState = Owner.PlayerPawn.Value.MovementServices.Buttons.ButtonStates[0];

                if (Server.CurrentTime > _extraJumpDelayTick && (buttonState & (ulong)PlayerButtons.Jump) != 0)
                {
                    Dash();
                    Owner.GetWarcraftPlayer().GetClass().StartCooldown(0);
                    this.Destroy();
                }
            }

            private void Dash()
            {
                ulong buttonState = Owner.PlayerPawn.Value.MovementServices.Buttons.ButtonStates[0];
                var directionAngle = Owner.PlayerPawn.Value.EyeAngles;

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

                Owner.PlayerPawn.Value.AbsVelocity.X = directionVec.X;
                Owner.PlayerPawn.Value.AbsVelocity.Y = directionVec.Y;
                Owner.PlayerPawn.Value.AbsVelocity.Z = directionVec.Z;

                Owner.EmitSound("Default.WalkJump", volume: 0.5f);
            }

            public override void OnFinish() { }
        }

        internal class EnsnaringTrapEffect(CCSPlayerController owner, float duration, Vector trapPosition) : WarcraftEffect(owner, duration)
        {
            private CPhysicsPropMultiplayer _trap;
            private Box3d _triggerZone;

            private bool IsTriggered { get; set; } = false;

            public override void OnStart()
            {
                //Beartrap model
                _trap = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
                _trap.Teleport(trapPosition, new QAngle(), new Vector());
                _trap.DispatchSpawn();
                _trap.SetModel("models/generic/street_trashcan_03/street_trashcan_03_lid_a.vmdl");
                _trap.SetColor(Color.FromArgb(60, 255, 255, 255));

                _triggerZone = Warcraft.CreateBoxAroundPoint(trapPosition, 100, 100, 100);
                //_triggerZone.Show(duration: Duration); //Debug
            }

            public override void OnTick()
            {
                if (!IsTriggered)
                {
                    //Find players in trap trigger zone
                    var players = Utilities.GetPlayers();
                    var playersInHurtZone = players.Where(x => x.PawnIsAlive && !x.AllyOf(Owner) && _triggerZone.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20))).ToList();
                    if (playersInHurtZone.Count != 0)
                    {
                        IsTriggered = true;
                        TriggerTrap(playersInHurtZone);
                    }
                }
            }

            private void TriggerTrap(List<CCSPlayerController> playersInTrap)
            {
                Warcraft.SpawnParticle(_trap.AbsOrigin.Clone().Add(z: 20), "particles/explosions_fx/explosion_hegrenade_water_ripple.vpcf", 1);
                //Show trap
                _trap.SetColor(Color.FromArgb(255, 255, 255, 255));
                //Set movement speed + small hurt
                foreach (var player in playersInTrap)
                {
                    player.TakeDamage(Owner.GetWarcraftPlayer().GetAbilityLevel(1) * 10, Owner, KillFeedIcon.tripwirefire);
                    player.PlayerPawn.Value.VelocityModifier = 0;
                    player.PlayerPawn.Value.MovementServices.Maxspeed = 20;
                    Warcraft.SpawnParticle(player.CalculatePositionInFront(10, 60), "particles/blood_impact/blood_impact_basic.vpcf");
                    player.PrintToChat($" {Localizer["ranger.trappedby", Owner.PlayerName]}");
                    Owner.PrintToChat($" {Localizer["ranger.trapowner", Owner.GetWarcraftPlayer().GetClass().GetAbility(1).DisplayName, player.GetRealPlayerName()]}");
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
            }
        }

        internal class ArrowStormEffect(CCSPlayerController owner, float duration, Vector stormpos) : WarcraftEffect(owner, duration)
        {
            private readonly int _stormHeight = 150;
            private readonly int _stormArea = 280;
            private readonly int _arrowsPerVolley = 15;
            private Box3d _arrowSpawnBox;
            private Box3d _hurtBox;

            public override void OnStart()
            {
                var spawnBoxPoint = stormpos.With(z: stormpos.Z + _stormHeight);
                _arrowSpawnBox = Warcraft.CreateBoxAroundPoint(spawnBoxPoint, _stormArea, _stormArea, 50);

                var hurtBoxPoint = stormpos.With(z: stormpos.Z + _stormHeight / 2);
                _hurtBox = Warcraft.CreateBoxAroundPoint(hurtBoxPoint, _stormArea, _stormArea, _stormHeight);
                //_hurtBox.Show(duration: Duration); //Debug
                Owner.EmitSound("UI.DeathMatch.Dominating", volume: 0.5f);
            }

            public override void OnTick()
            {
                Warcraft.SpawnParticle(_arrowSpawnBox.Center.ToVector().Add(z: 20), "particles/explosions_fx/explosion_hegrenade_water_ripple.vpcf", 1);

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
                var playersInHurtZone = players.Where(x => x.IsAlive() && !x.AllyOf(Owner) && _hurtBox.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20))).ToList();
                //Set movement speed + small hurt
                foreach (var player in playersInHurtZone)
                {
                    if (!player.IsAlive()) continue;

                    player.TakeDamage(4, Owner, KillFeedIcon.flair0);
                    player.PlayerPawn.Value.VelocityModifier = 0;
                    Warcraft.SpawnParticle(player.CalculatePositionInFront(10, 60), "particles/blood_impact/blood_impact_basic.vpcf");
                }
            }

            private void SpawnArrow()
            {
                //Calculate new arrow pos
                var arrowSpawn = _arrowSpawnBox.GetRandomPoint();
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