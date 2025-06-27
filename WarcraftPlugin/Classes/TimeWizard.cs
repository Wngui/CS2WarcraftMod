using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using g3;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace WarcraftPlugin.Classes
{
    internal class TimeWizard : WarcraftClass
    {
        private TemporalShiftEffect _temporalShiftEffect;

        public override string DisplayName => "Time Wizard";

        public override List<string> PreloadResources =>
        [
            "models/props/de_dust/hr_dust/dust_soccerball/dust_soccer_ball001.vmdl",
        ];

        //shiny particles/ui/ui_gold_halo_flare.vpcf
        //Blue portalish particles/ui/hud/ui_mvp_winner_burst.vpcf

        public override Color DefaultColor => Color.MediumPurple;

        public override List<IWarcraftAbility> Abilities =>
          [
            new WarcraftAbility("Time Acceleration", "Speeds up yourself and nearby allies."),
            new WarcraftAbility("Temporal Shift", "Chance to teleport back when hit."),
            new WarcraftAbility("Chrono Orb", "Chance to shoot orbs that deal damage and slow enemies."),
            new WarcraftCooldownAbility("Time Dilation", "Slows players inside for a short duration.", 50f)
          ];

        public override void Register()
        {
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventPlayerHurt>(PlayerHurt);
            HookEvent<EventWeaponFire>(PlayerShoot);

            HookAbility(3, Ultimate);
        }

        private void PlayerShoot(EventWeaponFire fire)
        {
            new ChronoOrbEffect(Player, 20).Start();
        }

        private void PlayerHurt(EventPlayerHurt hurt)
        {
            if (Warcraft.RollDice(WarcraftPlayer.GetAbilityLevel(1), 30))
            {
                _temporalShiftEffect?.TeleportBack();
            }
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            if (WarcraftPlayer.GetAbilityLevel(0) > 1)
                new TimeAccelerationEffect(Player).Start();

            if (WarcraftPlayer.GetAbilityLevel(1) > 1)
            {
                _temporalShiftEffect = new TemporalShiftEffect(Player);
                _temporalShiftEffect.Start();
            }

            Server.NextFrame(() =>
            {
                //Build tool
                var chronoOrbLevel = WarcraftPlayer.GetAbilityLevel(2);
                if (chronoOrbLevel > 0)
                {
                    var chronoOrbGun = new CWeaponTaser(Player.GiveNamedItem("weapon_taser"));
                    chronoOrbGun.AttributeManager.Item.CustomName = "Chrono Orb Gun";//Localizer["dwarf_engineer.buildtool"];
                    chronoOrbGun.Clip1 = chronoOrbLevel * 3;
                    chronoOrbGun.ReserveAmmo.Clear();
                    chronoOrbGun.SetColor(Color.Purple);
                }
            });
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;
        }
    }

    internal class TemporalShiftEffect(CCSPlayerController owner) : WarcraftEffect(owner, onTickInterval: 5f)
    {
        Vector _lastAbsOrigin;
        QAngle _lastAbsRotation;

        public override void OnStart()
        {
            Console.WriteLine($"TemporalShiftEffect started for {Owner.PlayerName}");
        }

        public override void OnTick()
        {
            if (Owner.IsAlive())
            {
                _lastAbsOrigin = Owner.PlayerPawn.Value.AbsOrigin;
                _lastAbsRotation = Owner.PlayerPawn.Value.AbsRotation;
            }
        }

        public void TeleportBack()
        {
            Warcraft.SpawnParticle(Owner.PlayerPawn.Value.AbsOrigin, "particles/ui/hud/ui_mvp_winner_burst.vpcf", 3);
            Owner.PlayerPawn.Value.Teleport(_lastAbsOrigin, _lastAbsRotation, null);
            Warcraft.SpawnParticle(Owner.PlayerPawn.Value.AbsOrigin, "particles/ui/hud/ui_mvp_winner_burst.vpcf", 3);
            Console.WriteLine($"{Owner.PlayerName} teleported back to {_lastAbsOrigin}");
        }

        public override void OnFinish()
        {
            Console.WriteLine($"TemporalShiftEffect finished for {Owner.PlayerName}");
        }
    }

    internal class TimeAccelerationEffect(CCSPlayerController owner) : WarcraftEffect(owner, onTickInterval: 1f)
    {
        private readonly float _maxSpeed = 1.5f + (float)Random.Shared.NextDouble() * 0.1f; //Unique speed to identify players that are already accelerated

        public override void OnStart()
        {
            Console.WriteLine($"TimeAccelerationEffect started for {Owner.PlayerName} with max speed {_maxSpeed}");
        }

        public override void OnTick()
        {
            var currentAbilityLevel = Owner.GetWarcraftPlayer().GetAbilityLevel(0);
            var auraSize = currentAbilityLevel * 100;
            var accelerationZone = Warcraft.CreateBoxAroundPoint(Owner.PlayerPawn.Value.AbsOrigin, auraSize, auraSize, auraSize);
            //accelerationZone.Show(duration: 2); //Debug
            //Find players within area
            var playersToAccelerate = AccelerateAllies(accelerationZone);
            DeaccelerateAllies(playersToAccelerate);
        }

        private List<CCSPlayerController> AccelerateAllies(Box3d accelerationZone)
        {
            var playersToAccelerate = Utilities.GetPlayers().Where(x =>
                x.AllyOf(Owner) &&
                x.PawnIsAlive &&
                Owner.IsValid &&
                accelerationZone.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20))
            ).ToList();

            foreach (var player in playersToAccelerate)
            {
                if (player.PlayerPawn.Value.VelocityModifier < _maxSpeed)
                    player.PlayerPawn.Value.VelocityModifier = _maxSpeed;
            }

            return playersToAccelerate;
        }

        private void DeaccelerateAllies(List<CCSPlayerController> playersToAccelerate)
        {
            var playersToDecelerate = Utilities.GetPlayers().Where(x =>
                x.AllyOf(Owner) &&
                x.PawnIsAlive &&
                Owner.IsValid &&
                x.PlayerPawn.Value.VelocityModifier == _maxSpeed &&
                !playersToAccelerate.Contains(x)
            );

            foreach (var player in playersToAccelerate)
            {
                player.PlayerPawn.Value.VelocityModifier = 1f;
            }
        }

        public override void OnFinish()
        {
            DeaccelerateAllies([]);
        }
    }

    internal class ChronoOrbEffect(CCSPlayerController owner, float duration) : WarcraftEffect(owner, duration, onTickInterval: 1)
    {
        private CPhysicsPropMultiplayer _chronoOrb;
        private CParticleSystem _particleTrail;
        private CParticleSystem _particleAOE;
        private CParticleSystem _particleInner;
        private readonly List<CCSPlayerController> _playersSlowed = [];

        private const float auraSize = 500; //Size of the AOE effect
        private const float slowModifier = 3f; //Slow modifier for players inside the orb
        private const float _orbDamage = 1f;

        public override void OnStart()
        {
            var orbPosition = Owner.CalculatePositionInFront(10, 60);

            _particleTrail = Warcraft.SpawnParticle(orbPosition, "particles/maps/de_shacks/shacks_policelight_blue_core.vpcf", Duration);
            _particleAOE = Warcraft.SpawnParticle(orbPosition, "particles/ui/ui_gold_award_tier_2_flare.vpcf", Duration);
            _particleInner = Warcraft.SpawnParticle(orbPosition, "particles/ambient_fx/survival_bunker_light.vpcf", Duration);
            _particleInner.StartTime = Server.CurrentTime - 5; //Start inner particle immediately

            //Create crono orb prop
            Vector velocity = Owner.CalculateVelocityAwayFromPlayer(500);

            _chronoOrb = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
            _chronoOrb.Teleport(orbPosition, null, velocity);
            _chronoOrb.DispatchSpawn();
            _particleTrail.SetParent(_chronoOrb);
            _particleAOE.SetParent(_chronoOrb);
            _particleInner.SetParent(_chronoOrb);
            _chronoOrb.SetModel("models/props/de_dust/hr_dust/dust_soccerball/dust_soccer_ball001.vmdl");
            _chronoOrb.AcceptInput("DisableGravity", _chronoOrb, _chronoOrb);
            _chronoOrb.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_WEAPON; //Dont collide with players
            _chronoOrb.SetColor(Color.FromArgb(140, 0, 0, 0));

            //Breakable
            _chronoOrb.Health = 50;
            _chronoOrb.TakeDamageFlags = TakeDamageFlags_t.DFLAG_NONE;
            _chronoOrb.ExplodeRadius = 1f;

            Owner.EmitSound("Door.wood_full_open", volume: 0.5f);
        }

        public override void OnTick()
        {
            if (!_chronoOrb.IsValid) { this.Destroy(); return; }

            var orbZone = Warcraft.CreateBoxAroundPoint(_chronoOrb.AbsOrigin, auraSize, auraSize, auraSize);

            var playersInOrbZone = Utilities.GetPlayers().Where(x =>
                //!x.AllyOf(Owner) &&
                x.IsAlive() &&
                orbZone.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20))
            ).ToList();

            var playersOutOrbZone = _playersSlowed.Except(playersInOrbZone).ToList();

            //Slow and damage players inside the orb zone
            foreach (var player in playersInOrbZone)
            {
                if (!player.IsAlive()) continue;
                if (!_playersSlowed.Contains(player))
                {
                    player.PrintToChat("You are slowed by the Chrono Orb!");
                    player.PlayerPawn.Value.Friction = slowModifier; //Apply slow
                    _playersSlowed.Add(player);
                }

                Warcraft.DrawLaserBetween(_chronoOrb.AbsOrigin, player.EyePosition(-40), Color.FromArgb(128, 128, 0, 255), 0.2f, 0.5f);
                player.TakeDamage(_orbDamage, Owner, KillFeedIcon.flair0);
            }

            //Remove slow from players that left the orb zone
            foreach (var player in playersOutOrbZone)
            {
                if (!player.IsAlive()) continue;
                player.PrintToChat("You are no longer slowed by the Chrono Orb!");
                player.PlayerPawn.Value.Friction = 1;
                _playersSlowed.Remove(player);
            }
        }

        public override void OnFinish()
        {
            if (_chronoOrb.IsValid)
            {
                _particleAOE?.RemoveIfValid();
                _particleInner?.RemoveIfValid();
                _particleTrail?.RemoveIfValid();
                _chronoOrb.AcceptInput("break");
            }

            foreach (var player in _playersSlowed)
            {
                if (!player.IsAlive()) continue;
                player.PlayerPawn.Value.Friction = 1;
            }
        }
    }
}