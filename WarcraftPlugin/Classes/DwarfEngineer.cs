using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using System.Drawing;
using WarcraftPlugin.Core.Effects;
using System.Collections.Generic;
using System;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace WarcraftPlugin.Classes
{
    internal class DwarfEngineer : WarcraftClass
    {
        private readonly int _stoneSkinArmorMultiplier = 20;
        private BuildEffect _buildEffect;

        public override string DisplayName => "Dwarf Engineer";

        public override List<string> PreloadResources =>
        [
            "models/props/de_aztec/hr_aztec/aztec_walls/aztec_ground_rock_01_rock_01.vmdl",
            "models/food/fruits/banana01a.vmdl",
            ..BuildEffect.Props.Select(p => p.ModelPath)
        ];

        public override Color DefaultColor => Color.FromArgb(0, 100, 255); //Diamond blue

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Build", "Allows you to build using the builder tool with 3/6/9/12/15 charges."),
            new WarcraftAbility("Pickaxe", "6/12/18/24/30% chance to find grenades when stabbing surfaces."),
            new WarcraftAbility("Stone Skin", "Increase armor by 20/40/60/80/100."),
            new WarcraftCooldownAbility("Goldrush!", "Gain 1000 HP and a speed boost for 10s", 50f)
        ];

        public override void Register()
        {
            HookEvent<EventWeaponFire>(PlayerShoot);
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventItemEquip>(PlayerItemEquip);

            HookAbility(3, Ultimate);
        }

        private void PlayerItemEquip(EventItemEquip equip)
        {
            var activeWeapon = Player.PlayerPawn.Value.WeaponServices?.ActiveWeapon.Value;

            if (activeWeapon != null && activeWeapon.IsValid && activeWeapon.AttributeManager.Item.CustomName == "Build Tool")
            {
                _buildEffect = new BuildEffect(Player);
                _buildEffect.Start();
            }
            else
            {
                _buildEffect?.Destroy();
            }
        }

        private void PlayerShoot(EventWeaponFire @event)
        {
            var activeWeapon = Player.PlayerPawn.Value.WeaponServices?.ActiveWeapon.Value;

            if (activeWeapon != null && activeWeapon.IsValid)
            {
                //Mining: Chance to find grenades when stabbing surfaces
                if (WarcraftPlayer.GetAbilityLevel(1) > 0 && activeWeapon.DesignerName == "weapon_knife")
                {
                    Pickaxe();
                }

                if (activeWeapon.AttributeManager.Item.CustomName == "Build Tool")
                {
                    _buildEffect?.SpawnProp();
                }
            }
        }

        private void Pickaxe()
        {
            var trace = Player.RayTrace();
            var isCloseToMineableWall = (trace - Player.EyePosition()).Length() < 71;

            if (isCloseToMineableWall)
            {
                var success = Warcraft.RollDice(WarcraftPlayer.GetAbilityLevel(1), 30);
                new PickAxeEffect(Player, 10, success, trace).Start();
            }
        }

        private void PlayerSpawn(EventPlayerSpawn @event)
        {
            Server.NextFrame(() =>
            {
                //Steve is small
                Player.PlayerPawn.Value.SetScale(0.8f);

                //Build tool
                var buildLevel = WarcraftPlayer.GetAbilityLevel(0);
                if (buildLevel > 0)
                {
                    Player.RemoveWeaponBySlot(gear_slot_t.GEAR_SLOT_PISTOL);
                    var builderTool = new CWeaponFiveSeven(Player.GiveNamedItem("weapon_fiveseven"));
                    builderTool.AttributeManager.Item.CustomName = Localizer["dwarf_engineer.buildtool"];
                    builderTool.Clip1 = buildLevel * 3;
                    builderTool.ReserveAmmo.Clear();
                    builderTool.SetColor(Color.Red);
                }

                //Pickaxe
                if (WarcraftPlayer.GetAbilityLevel(1) > 0)
                {
                    var pickaxe = Player.GetWeaponBySlot(gear_slot_t.GEAR_SLOT_KNIFE);
                    pickaxe.SetColor(Color.FromArgb(0, 100, 255));
                    pickaxe.AttributeManager.Item.CustomName = Localizer["dwarf_engineer.pickaxe"];
                }

                //Diamond armor
                if (WarcraftPlayer.GetAbilityLevel(2) > 0)
                {
                    Server.NextFrame(() =>
                    {
                        Player.GiveNamedItem("item_assaultsuit");
                        var armorBonus = WarcraftPlayer.GetAbilityLevel(2) * _stoneSkinArmorMultiplier;
                        Player.SetArmor(100 + armorBonus);
                        Player.PrintToChat($" {ChatColors.Green}{GetAbility(2).DisplayName}{ChatColors.Default} +{armorBonus} armor");
                    });
                }

                Server.NextFrame(() =>
                {
                    // Dirty fix to ensure color/equip events are processed
                    Player.ExecuteClientCommand("slot3");
                });
            });
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            new GoldRushEffect(Player, 10, 1000).Start();
            StartCooldown(3);
        }
    }

    internal class PickAxeEffect(CCSPlayerController owner, float duration, bool success, Vector trace) : WarcraftEffect(owner, duration, onTickInterval: 1f)
    {
        private static readonly string[] MineDrops =
        [
            "weapon_flashbang",
            "weapon_smokegrenade",
            "weapon_hegrenade",
            "weapon_molotov",
            "weapon_decoy"
        ];

        private static readonly List<Color> DropColors =
        [
            Color.Red,
            Color.Green,
            Color.Blue,
            Color.Cyan,
            Color.Yellow,
            Color.Purple,
            Color.Orange,
            Color.DeepPink,
            Color.Gold
        ];

        private CBaseModelEntity _weaponDropEntity;
        private CDynamicProp _junkProp;

        public override void OnStart()
        {
            Warcraft.SpawnParticle(trace, "particles/ambient_fx/ambient_sparks_core.vpcf", 1);

            if (success)
            {
                var grenadeName = MineDrops[Random.Shared.Next(MineDrops.Length)];

                // Workaround to spawn grenade, prevent pickup for a moment
                Owner.PlayerPawn.Value.WeaponServices.PreventWeaponPickup = true;
                var weaponDrop = new CCSWeaponBaseGun(Owner.GiveNamedItem(grenadeName));
                Owner.PlayerPawn.Value.WeaponServices.PreventWeaponPickup = false;
                weaponDrop.CanBePickedUp = false;

                weaponDrop.SetColor(DropColors[Random.Shared.Next(DropColors.Count)]);

                _weaponDropEntity = weaponDrop;
            }
            else
            {
                // Hide the weapon drop and show junk instead
                _weaponDropEntity = Utilities.CreateEntityByName<CPhysicsPropMultiplayer>("prop_physics_multiplayer");
                _weaponDropEntity.DispatchSpawn();
                _weaponDropEntity.SetModel("models/food/fruits/banana01a.vmdl");
                _weaponDropEntity.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_WEAPON;
                _weaponDropEntity.SetColor(Color.FromArgb(0, 0, 0, 0)); // Make it invisible

                _junkProp = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
                _junkProp.DispatchSpawn();
                _junkProp.SetModel("models/props/de_aztec/hr_aztec/aztec_walls/aztec_ground_rock_01_rock_01.vmdl");
                _junkProp.SetScale(Random.Shared.NextSingle() * 0.3f + 0.2f);
                _junkProp.Teleport(_weaponDropEntity.AbsOrigin, null, null);
                _junkProp.SetParent(_weaponDropEntity);
            }

            Server.NextFrame(() =>
            {
                _weaponDropEntity.Teleport(trace, new QAngle(
                Random.Shared.NextSingle() * 360f,
                Random.Shared.NextSingle() * 360f,
                Random.Shared.NextSingle() * 360f
                ), Warcraft.CalculateTravelVelocity(trace, Owner.EyePosition(), 0.5f));
            });
        }

        public override void OnTick()
        {
            if (!_weaponDropEntity.IsValid) { this.Destroy(); return; }
            if (success)
            {
                (_weaponDropEntity as CCSWeaponBaseGun).CanBePickedUp = true;
            }
            else
            {
                _junkProp.SetColor(Color.FromArgb(
                    Math.Max(0, _junkProp.Render.A - (int)(255 / Duration)),
                    _junkProp.Render.R,
                    _junkProp.Render.G,
                    _junkProp.Render.B
                ));
            }
        }

        public override void OnFinish()
        {
            if (!success)
            {
                _weaponDropEntity.RemoveIfValid();
            }
        }
    }

    internal class BuildEffect(CCSPlayerController owner) : WarcraftEffect(owner, onTickInterval: 0.01f)
    {
        internal class Prop
        {
            public string ModelPath { get; set; }
            public Vector Offset { get; set; } = new Vector(0, 0, 0);
        }

        internal static readonly List<Prop> Props =
        [
            new Prop{ModelPath = "models/props/de_dust/hr_dust/dust_crates/dust_crate_style_01_32x32x32.vmdl"},
            new Prop{ModelPath = "models/props/de_dust/dust_rusty_barrel.vmdl" },
            new Prop{ModelPath = "models/props/de_inferno/hr_i/barrel_a/barrel_a_full.vmdl" },
            new Prop{ModelPath = "models/de_anubis/trims/mudbrick_roof_trim01_128.vmdl", Offset = new (60,0,0)},
            new Prop{ModelPath = "models/de_anubis/trims/mudbrick_roof_trim01a_64_01.vmdl", Offset = new (30,0,0)},
            new Prop{ModelPath = "models/props/hr_massive/wood_fence/wood_fence_128.vmdl"},
            new Prop{ModelPath = "models/props/de_vertigo/wood_pallet_01.vmdl",Offset = new (0,0,20)},
            new Prop{ModelPath = "models/props/de_inferno/chairantique_static.vmdl"},
            new Prop{ModelPath = "models/props/de_inferno/furniturecouch001a.vmdl"},
            new Prop{ModelPath = "models/props/cs_office/vending_machine.vmdl"},
            new Prop{ModelPath = "models/chicken/chicken_roasted.vmdl",Offset = new (0,0,20)},
            new Prop{ModelPath = "models/de_overpass/stuffed_animals/stuffed_elephant.vmdl"},
            new Prop{ModelPath = "weapons/models/c4/weapon_c4.vmdl", Offset = new (0,0,30)},
        ];

        private int _currentPropIndex = 0;
        private CPhysicsPropOverride _blueprintProp;
        private float _lastActionTick = Server.CurrentTime;
        private float _yawOffset = 0f;

        public override void OnStart()
        {
            Server.NextFrame(() =>
            {
                Owner.PrintToChat(" " + Localizer["dwarf_engineer.build.tooltip"]);
            });

            var currentDef = Props[_currentPropIndex];

            _blueprintProp = Utilities.CreateEntityByName<CPhysicsPropOverride>("prop_physics_override");
            _blueprintProp.Teleport(BlueprintPosition(), Owner.PlayerPawn.Value.EyeAngles, new Vector());
            _blueprintProp.DispatchSpawn();
            _blueprintProp.SetModel(currentDef.ModelPath);
            _blueprintProp.AcceptInput("DisableGravity", _blueprintProp, _blueprintProp);
            _blueprintProp.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_NEVER;
            _blueprintProp.SetColor(Color.FromArgb(150, 255, 255, 255));
        }

        public override void OnTick()
        {
            Vector velocity = Warcraft.CalculateTravelVelocity(_blueprintProp.AbsOrigin, BlueprintPosition(), 0.1f);
            var eyeAngles = Owner.PlayerPawn.Value.EyeAngles;
            QAngle rotation = new(0, eyeAngles.Y + _yawOffset, 0);
            _blueprintProp.Teleport(null, rotation, velocity);

            if (Server.CurrentTime - _lastActionTick < 0.3) return;

            ulong buttonState = Owner.PlayerPawn.Value.MovementServices.Buttons.ButtonStates[0];

            if ((buttonState & (ulong)PlayerButtons.Attack2) != 0)
            {
                _lastActionTick = Server.CurrentTime;
                NextBlueprintProp();
            }

            if ((buttonState & (ulong)PlayerButtons.Reload) != 0)
            {
                _lastActionTick = Server.CurrentTime;
                _yawOffset += 20;
            }

            var activeWeapon = Owner.PlayerPawn.Value.WeaponServices?.ActiveWeapon.Value;
            if (activeWeapon == null || !activeWeapon.IsValid || activeWeapon.AttributeManager.Item.CustomName != "Build Tool")
                Destroy();
        }

        public override void OnFinish()
        {
            _blueprintProp.RemoveIfValid();
        }

        private void NextBlueprintProp()
        {
            _yawOffset = 0;
            _currentPropIndex = (_currentPropIndex + 1) % Props.Count;
            _blueprintProp.SetModel(Props[_currentPropIndex].ModelPath);
            _blueprintProp.Teleport(BlueprintPosition(), Owner.PlayerPawn.Value.EyeAngles, new Vector());
        }

        public void SpawnProp()
        {
            Server.NextFrame(() =>
            {
                var currentDef = Props[_currentPropIndex];

                var prop = Utilities.CreateEntityByName<CPhysicsPropOverride>("prop_physics_override");
                prop.DispatchSpawn();
                prop.Teleport(_blueprintProp.AbsOrigin, _blueprintProp.AbsRotation);
                prop.SetModel(currentDef.ModelPath);

                Server.NextWorldUpdate(() =>
                {
                    prop.Collision.CollisionAttribute.InteractsAs = 136446081;
                    prop.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_PUSHAWAY;
                    prop.Health = 50;
                    prop.TakeDamageFlags = TakeDamageFlags_t.DFLAG_NONE;
                    prop.ExplodeRadius = 1f;
                });
            });
        }

        private Vector BlueprintPosition()
        {
            var pawn = Owner.PlayerPawn.Value;
            Vector pos = pawn.AbsOrigin.Clone();
            Vector forward = new(), right = new(), up = new();

            NativeAPI.AngleVectors(pawn.EyeAngles.Handle, forward.Handle, right.Handle, up.Handle);

            pos.Z += Owner.EyeHeight() - 20;
            pos += forward * 70f;

            // Apply local offset based on model definition
            Vector offset = Props[_currentPropIndex].Offset;
            pos += right * offset.X;
            pos += forward * offset.Y;
            pos += up * offset.Z;

            return pos;
        }
    }


    internal class GoldRushEffect(CCSPlayerController owner, float duration, int healthBonus) : WarcraftEffect(owner, duration)
    {
        public override void OnStart()
        {
            Warcraft.SpawnParticle(Owner.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/bumpmine_detonate_distort.vpcf", 1);
            Owner.AdrenalineSurgeEffect(Duration);
            Owner.PlayerPawn.Value.SetColor(Color.Gold);
            Owner.EmitSound("BaseGrenade.JumpThrowM", volume: 0.5f);
            Owner.SetHp(healthBonus);
            Owner.Blind(Duration - 5, Color.FromArgb(50, 255, 255, 0));
        }

        public override void OnTick()
        {
            if (!Owner.IsAlive()) return;
        }

        public override void OnFinish()
        {
            if (!Owner.IsAlive()) return;

            var pawn = Owner.PlayerPawn.Value;
            Owner.GetWarcraftPlayer().GetClass().SetDefaultAppearance();
            Owner.Unblind();
            Owner.SetHp(pawn.MaxHealth);
        }
    }
}