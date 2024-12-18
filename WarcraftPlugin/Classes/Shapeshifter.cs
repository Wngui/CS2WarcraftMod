﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WarcraftPlugin.Core;
using WarcraftPlugin.Effects;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using static CounterStrikeSharp.API.Core.Listeners;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace WarcraftPlugin.Classes
{
    public class Shapeshifter : WarcraftClass
    {
        public override string InternalName => "shapeshifter";
        public override string DisplayName => "Shapeshifter";
        public override DefaultClassModel DefaultModel => new()
        {
            //TModel = "characters/models/tm_phoenix/tm_phoenix_varianti.vmdl",
            //CTModel = "characters/models/ctm_swat/ctm_swat_variantj.vmdl"
        };
        public override Color DefaultColor => Color.Pink;

        private bool _isShapeshifted = false;
        private bool _isDisguised = false;
        private CDynamicProp _playerShapeshiftProp;
        private CDynamicProp _cameraProp;
        private Timer _checkSpottedTimer;
        private string _initialPlayerModel;
        private readonly List<string> _weaponList = [];

        public override void Register()
        {
            AddAbility(new WarcraftAbility("adaptive_disguise", "Adaptive Disguise",
                i => $"Chance to spawn with an enemy disguise, revealed upon attacking."));

            AddAbility(new WarcraftAbility("doppelganger", "Doppelganger",
                i => $"Create a temporary inanimate clone of yourself, using a decoy grenade."));

            AddAbility(new WarcraftAbility("imposter_syndrom", "Imposter syndrom",
                i => $"Chance to be notified when revealed by enemies on radar."));

            AddAbility(new WarcraftCooldownAbility("morphling", "Morphling",
                i => $"Transform into an unassuming object.",
                20f));

            HookAbility(3, Ultimate);

            HookEvent<EventWeaponFire>("player_shoot", PlayerShoot);
            HookEvent<EventPlayerHurt>("player_hurt", PlayerHurt);
            HookEvent<EventPlayerDeath>("player_death", PlayerDeath);
            HookEvent<EventRoundEnd>("round_end", RoundEnd);
            HookEvent<EventPlayerSpawn>("player_spawn", PlayerSpawn);
            HookEvent<EventDecoyStarted>("decoy_start", DecoyStart);
        }

        private void DecoyStart(EventDecoyStarted decoy)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                Utilities.GetEntityFromIndex<CDecoyProjectile>(decoy.Entityid)?.Remove();
                DispatchEffect(new CloneDecoyEffect(Player, 5 * WarcraftPlayer.GetAbilityLevel(1), new Vector(decoy.X, decoy.Y, decoy.Z)));
            }
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            _initialPlayerModel = Player.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().ModelState.ModelName;
            BreakTransformation();

            //Disguise
            if (WarcraftPlayer.GetAbilityLevel(0) > 0)
            {
                double rolledValue = Random.Shared.NextDouble();
                float chanceToDisguide = WarcraftPlayer.GetAbilityLevel(0) / 5 * 0.40f;

                if (rolledValue <= chanceToDisguide)
                {
                    Disguise();
                }
            }

            //Doppelganger
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                var decoy = new CDecoyGrenade(Player.GiveNamedItem("weapon_decoy"));
                decoy.AttributeManager.Item.CustomName = "Doppelganger decoy";
            }

            //Imposter syndrom
            if (WarcraftPlayer.GetAbilityLevel(2) > 0)
                _checkSpottedTimer = WarcraftPlugin.Instance.AddTimer(1, CheckIfSpotted, TimerFlags.REPEAT);
        }

        private void CheckIfSpotted()
        {
            if (!Player.IsValid || !Player.PawnIsAlive)
            {
                _checkSpottedTimer?.Kill();
                return;
            }

            if (Player.PlayerPawn.Value.EntitySpottedState.Spotted)
            {
                //disable timer and start timer again after cooldown
                _checkSpottedTimer?.Kill();
                WarcraftPlugin.Instance.AddTimer(5, () =>
                {
                    _checkSpottedTimer?.Kill();
                    _checkSpottedTimer = WarcraftPlugin.Instance.AddTimer(1, CheckIfSpotted, TimerFlags.REPEAT);
                });

                //chance
                double rolledValue = Random.Shared.NextDouble();
                float chanceToAlert = WarcraftPlayer.GetAbilityLevel(2) / 5f;

                if (rolledValue <= chanceToAlert)
                {
                    Player.PrintToCenter($"[Spotted]");
                    Player.PlayLocalSound("sounds/ui/panorama/ping_alert_01.vsnd");
                    Player.PlayerPawn.Value.HealthShotBoostExpirationTime = Server.CurrentTime + 0.2f;
                    Utilities.SetStateChanged(Player.PlayerPawn.Value, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
                }
            }
        }

        private void Disguise()
        {
            var teamToDisguise = Player.Team == CsTeam.CounterTerrorist ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
            var enemyplayer = Utilities.GetPlayers().Where(x => x.Team == teamToDisguise && x.PawnIsAlive).FirstOrDefault();

            if (enemyplayer == null)
            {
                Player.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Red}Disguise failed{ChatColors.Default}, no enemies to copy", 1);
                return;
            }

            Player.PlayerPawn.Value.SetColor(Color.White);

            Player.PlayerPawn.Value.SetModel(enemyplayer?.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().ModelState.ModelName);
            Player.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Blue}Disguised{ChatColors.Default} as {teamToDisguise}", 1);
            Player.PlayLocalSound("sounds/ui/armsrace_final_kill_knife.vsnd");
            _isDisguised = true;
        }

        private void PlayerHurt(EventPlayerHurt hurt)
        {
            UnShapeshift();
        }

        private void RoundEnd(EventRoundEnd end)
        {
            BreakTransformation();
            _checkSpottedTimer?.Kill();
        }

        private void PlayerDeath(EventPlayerDeath death)
        {
            BreakTransformation();
            _checkSpottedTimer?.Kill();
        }

        public override void PlayerChangingToAnotherRace()
        {
            BreakTransformation();
            _checkSpottedTimer?.Kill();
        }

        private void PlayerShoot(EventWeaponFire fire)
        {
            BreakTransformation();
        }

        private void BreakTransformation()
        {
            if (_isShapeshifted)
            {
                UnShapeshift();
            }
            else if (_isDisguised)
            {
                UnDisguise();
            }
        }

        private void UnDisguise()
        {
            SetDefaultAppearance();

            _isDisguised = false;

            Player.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Red}Disguise{ChatColors.Default} broken!", 1);
        }

        private void UnShapeshift()
        {
            _playerShapeshiftProp?.RemoveIfValid();

            SetDefaultAppearance();

            _isShapeshifted = false;

            //unattach camera & listener
            UnhookCamera();
            WeaponRestore();
        }

        private void UnhookCamera()
        {
            var onTick = new OnTick(UpdateCamera);
            WarcraftPlugin.Instance.RemoveListener(onTick);

            Player.PlayerPawn.Value.CameraServices.ViewEntity.Raw = uint.MaxValue;
            Utilities.SetStateChanged(Player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");

            _cameraProp?.RemoveIfValid();
            _cameraProp = null;
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            Shapeshift();
        }

        private void Shapeshift()
        {
            //Check if reshapeshifting
            if (_isShapeshifted)
            {
                _playerShapeshiftProp.SetModel(Props[Random.Shared.Next(Props.Count - 1)]);
                return;
            }

            BreakTransformation();
            WeaponStrip();

            Player.PlayerPawn.Value.SetColor(Color.FromArgb(0, 255, 255, 255));

            _cameraProp = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            _cameraProp.DispatchSpawn();
            _cameraProp.SetColor(Color.FromArgb(0, 255, 255, 255));

            _playerShapeshiftProp = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
            _playerShapeshiftProp.DispatchSpawn();

            _playerShapeshiftProp.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NEVER;
            _playerShapeshiftProp.Collision.SolidFlags = 12;
            _playerShapeshiftProp.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
            _playerShapeshiftProp.Spawnflags = 256U;

            _playerShapeshiftProp.SetModel(Props[Random.Shared.Next(Props.Count - 1)]);

            _playerShapeshiftProp.Teleport(Player.PlayerPawn.Value.AbsOrigin, Player.PlayerPawn.Value.AbsRotation, new Vector());
            _playerShapeshiftProp.SetParent(Player.PlayerPawn.Value);

            Player.PlayerPawn.Value.CameraServices.ViewEntity.Raw = _cameraProp.EntityHandle.Raw;
            Utilities.SetStateChanged(Player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");
            Player.PlayLocalSound("sounds/player/footsteps/water_exit_01.vsnd");

            WarcraftPlugin.Instance.RegisterListener<OnTick>(UpdateCamera);

            _isShapeshifted = true;
        }

        private void UpdateCamera()
        {
            if (!_cameraProp.IsValid || !Player.PlayerPawn.IsValid || !Player.PawnIsAlive)
            {
                UnhookCamera();
                return;
            }

            //Unhook if attack button is pressed
            if ((Player.PlayerPawn.Value.MovementServices.Buttons.ButtonStates[0] & (ulong)PlayerButtons.Attack) == 1)
            {
                BreakTransformation();
                return;
            }

            _cameraProp.Teleport(Player.CalculatePositionInFront(new Vector(-110, 0, 90)), Player.PlayerPawn.Value.V_angle, new Vector());
        }

        private void WeaponStrip()
        {
            _weaponList.Clear();
            Player.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = true;

            Player.DropWeaponByDesignerName("weapon_c4");

            foreach (var weapon in Player.PlayerPawn.Value.WeaponServices.MyWeapons)
            {
                _weaponList.Add(weapon.Value!.DesignerName!);
            }

            Player.RemoveWeapons();
        }

        private void WeaponRestore()
        {
            Player.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = false;
            foreach (var weapon in _weaponList)
            {
                Player.GiveNamedItem(weapon);
            }
        }

        public static readonly List<string> Props =
        [
            "models/props/de_dust/dust_aid_crate_56.vmdl",
            "models/props/de_dust/dust_food_crates_56.vmdl",
            "models/props/de_dust/dust_rusty_barrel.vmdl",
            "models/props/de_dust/grainbasket01a.vmdl",
            "models/props/de_dust/grainbasket01c.vmdl",
            "models/props/de_dust/pallet02.vmdl",
            "models/props/de_dust/stoneblocks48.vmdl",
            "models/props/de_dust/wagon.vmdl",
            "models/props/de_inferno/claypot02.vmdl",
            "models/props/de_inferno/cinderblock.vmdl",
            "models/props/de_inferno/wire_spool01_new.vmdl",
            "models/props/hr_vertigo/vertigo_tools/toolbox_small.vmdl",
            "models/props/hr_vertigo/warning_barrel/warning_barrel.vmdl",
            "models/props/de_mirage/pillow_a.vmdl",
            "models/props/hr_massive/survival_carpets/survival_carpet_01.vmdl",
            "models/props/de_aztec/hr_aztec/aztec_stairs/aztec_stairs_01_loose_rock_03.vmdl",
            "models/props/cs_italy/bananna.vmdl",
            "models/props/de_nuke/crate_extrasmall.vmdl",
            "models/props/de_nuke/hr_nuke/nuke_cars/nuke_compact01.vmdl",
            "models/de_overpass/junk/cardboard_box/cardboard_box_4.vmdl",
            "models/de_overpass/overpass_cardboard_box/overpass_cardboard_box_01.vmdl",
            "models/de_inferno/wood_fruit_container/wood_fruit_container_01.vmdl",
            "models/props/de_aztec/hr_aztec/aztec_pottery/aztec_pottery_vase_03.vmdl",
            "models/props/de_aztec/hr_aztec/aztec_pottery/aztec_pottery_cup_01.vmdl",
            "models/props/de_dust/hr_dust/dust_pottery/dust_pottery_01.vmdl",
            "models/props/cs_office/computer_caseb.vmdl",
            "models/props/cs_office/file_box.vmdl",
            "models/props/cs_office/ladder_office.vmdl",
            "models/props/cs_office/plant01.vmdl",
            "models/props/cs_office/radio.vmdl",
            "models/props/cs_office/snowman_face.vmdl",
            "models/props/cs_office/sofa.vmdl",
            "models/props/cs_office/trash_can.vmdl",
            "models/props/cs_office/vending_machine.vmdl"
        ];
    }

    internal class CloneDecoyEffect(CCSPlayerController owner, float duration, Vector decoyVector) : WarcraftEffect(owner, duration)
    {
        private readonly Vector _decoyVector = decoyVector;
        private CPhysicsPropMultiplayer _clone;
        private CPhysicsPropMultiplayer _cloneDebrisHead;

        public override void OnStart()
        {
            _clone = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
            _clone.SetModel("models/generic/bust_02/bust_02_a.vmdl");
            _clone.DispatchSpawn();
            _clone.SetModel(Owner.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().ModelState.ModelName);
            _clone.Teleport(_decoyVector.With().Add(z: -2), new QAngle(0, Owner.PlayerPawn.Value.EyeAngles.Y, 0), new Vector());

            _cloneDebrisHead = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
            _cloneDebrisHead.SetModel("models/generic/bust_02/bust_02_a.vmdl");
            _cloneDebrisHead.DispatchSpawn();
            _cloneDebrisHead.Teleport(_decoyVector.With().Add(z: 35), new QAngle(), new Vector());
            _cloneDebrisHead.SetColor(Color.FromArgb(1, 255, 255, 255));
        }

        public override void OnTick()
        {
            if (!_clone.IsValid && _cloneDebrisHead.IsValid)
            {
                _cloneDebrisHead?.AcceptInput("Break");
            }

            if (!_cloneDebrisHead.IsValid)
            {
                _clone?.RemoveIfValid();
            }
        }

        public override void OnFinish()
        {
            _clone?.RemoveIfValid();

            if (_cloneDebrisHead.IsValid)
            {
                _cloneDebrisHead?.AcceptInput("Break");
            }
        }
    }
}