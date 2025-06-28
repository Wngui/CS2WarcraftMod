using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using g3;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using WarcraftPlugin.Models;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace WarcraftPlugin.Helpers
{
    /// <summary>
    /// The Warcraft class provides various utility methods for interacting with the game environment,
    /// including drawing laser beams, spawning particles and explosions, manipulating player attributes,
    /// and performing geometric calculations.
    /// </summary>
    public static class Warcraft
    {
        /// <summary>
        /// Draws a laser beam between two points with specified color, duration, and width.
        /// </summary>
        /// <param name="startPos">The starting position of the laser beam.</param>
        /// <param name="endPos">The ending position of the laser beam.</param>
        /// <param name="color">The color of the laser beam. Defaults to red if not specified.</param>
        /// <param name="duration">The duration for which the laser beam will be visible.</param>
        /// <param name="width">The width of the laser beam.</param>
        /// <returns>Returns the created CBeam object or null if creation failed.</returns>
        static public CBeam DrawLaserBetween(Vector startPos, Vector endPos, Color? color = null, float duration = 1, float width = 2)
        {
            CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null) return null;

            beam.Render = color ?? Color.Red;
            beam.Width = width;

            beam.Teleport(startPos, new QAngle(), new Vector());
            beam.EndPos.X = endPos.X;
            beam.EndPos.Y = endPos.Y;
            beam.EndPos.Z = endPos.Z;
            beam.DispatchSpawn();
            WarcraftPlugin.Instance.AddTimer(duration, () => beam.RemoveIfValid());
            return beam;
        }

        /// <summary>
        /// Gets the world eye position of the player.
        /// </summary>
        /// <param name="player">The player controller.</param>
        /// <param name="offset">Optional offset to add to the eye position.</param>
        /// <returns>Returns a Vector representing the eye height of the player.</returns>
        public static Vector EyePosition(this CCSPlayerController player, float offset = 0)
        {
            return player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: player.EyeHeight() + offset);
        }

        /// <summary>
        /// Gets the eye height of the player.
        /// </summary>
        /// <param name="player">The player controller.</param>
        /// <returns>Returns the eye height of the player.</returns>
        public static float EyeHeight(this CCSPlayerController player)
        {
            return player.PlayerPawn.Value.ViewOffset.Z;
        }

        /// <summary>
        /// Spawns a particle system at the specified position with a given particle effect name and duration.
        /// </summary>
        /// <param name="pos">The position to spawn the particle system.</param>
        /// <param name="particleName">The name of the particle effect.</param>
        /// <param name="duration">The duration for which the particle system will be active.</param>
        /// <returns>Returns the created CParticleSystem object or null if creation failed.</returns>
        public static CParticleSystem SpawnParticle(Vector pos, string particleName, float duration = 5)
        {
            CParticleSystem particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
            if (!particle.IsValid) return null;
            particle.EffectName = particleName;
            particle?.Teleport(pos, new QAngle(), new Vector());
            particle.StartActive = true;
            particle?.DispatchSpawn();

            WarcraftPlugin.Instance.AddTimer(duration, () => particle?.RemoveIfValid());

            return particle;
        }

        /// <summary>
        /// Spawns an explosion at the specified position with given damage and radius.
        /// </summary>
        /// <param name="pos">The position to spawn the explosion.</param>
        /// <param name="damage">The damage caused by the explosion.</param>
        /// <param name="radius">The radius of the explosion.</param>
        /// <param name="attacker">The player controller who caused the explosion.</param>
        /// <param name="killFeedIcon">The icon to display in the kill feed. Attacker must be set</param>
        public static void SpawnExplosion(Vector pos, float damage, float radius, CCSPlayerController attacker = null, KillFeedIcon? killFeedIcon = null)
        {
            var heProjectile = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");
            if (heProjectile == null || !heProjectile.IsValid) return;
            pos.Z += 10;
            heProjectile.TicksAtZeroVelocity = 100;
            heProjectile.Damage = damage;
            heProjectile.DmgRadius = radius;
            heProjectile.Teleport(pos, new QAngle(), new Vector(0, 0, -10));
            heProjectile.DispatchSpawn();
            heProjectile.AcceptInput("InitializeSpawnFromWorld", attacker.PlayerPawn.Value, attacker.PlayerPawn.Value, "");
            Schema.SetSchemaValue(heProjectile.Handle, "CBaseGrenade", "m_hThrower", attacker.PlayerPawn.Raw); //Fixes killfeed
            heProjectile.DetonateTime = 0;
            if (killFeedIcon != null) attacker?.GetWarcraftPlayer()?.GetClass()?.SetKillFeedIcon(killFeedIcon);
        }

        /// <summary>
        /// Spawns a smoke grenade at the specified position with given color.
        /// </summary>
        /// <param name="pos">The position to spawn the smoke grenade.</param>
        /// <param name="attacker">The player pawn who caused the smoke grenade.</param>
        /// <param name="color">The color of the smoke.</param>
        /// <returns>Returns the created CSmokeGrenadeProjectile object.</returns>
        public static CSmokeGrenadeProjectile SpawnSmoke(Vector pos, CCSPlayerPawn attacker, Color color)
        {
            var smokeProjectile = Memory.CSmokeGrenadeProjectile_CreateFunc.Invoke(
                        pos.Handle,
                        new Vector(0, 0, 0).Handle,
                        new Vector(0, 0, 0).Handle,
                        new Vector(0, 0, 0).Handle,
                        nint.Zero,
                        45,
                        attacker.TeamNum);
            //Smoke color
            smokeProjectile.SmokeColor.X = color.R;
            smokeProjectile.SmokeColor.Y = color.G;
            smokeProjectile.SmokeColor.Z = color.B;

            return smokeProjectile;
        }

        /// <summary>
        /// Emits a sound from the specified entity.
        /// </summary>
        /// <param name="entity">The entity emitting the sound.</param>
        /// <param name="soundpath">The path to the sound file.</param>
        /// <param name="pitch">The pitch of the sound.</param>
        /// <param name="volume">The volume of the sound.</param>
        /// <param name="delay">The delay before the sound is played.</param>
        public static void EmitSound(this CBaseEntity entity, string soundpath, int pitch = 1, float volume = 1, float delay = 0)
        {
            Memory.CBaseEntity_EmitSoundParamsFunc?.Invoke(entity, soundpath, pitch, volume, delay);
        }

        /// <summary>
        /// Attaches a child entity to a specified parent entity.
        /// </summary>
        /// <param name="childEntity">The child entity.</param>
        /// <param name="parentEntity">The parent entity.</param>
        public static void SetParent(this CBaseEntity childEntity, CBaseEntity parentEntity)
        {
            if (!childEntity.IsValid || !parentEntity.IsValid) return;

            var origin = new Vector(childEntity.AbsOrigin!.X, childEntity.AbsOrigin!.Y, childEntity.AbsOrigin!.Z);
            Memory.CBaseEntity_SetParent.Invoke(childEntity, parentEntity, null, null);
            // If not teleported, the childrenEntity will not follow the parentEntity correctly.
            childEntity.Teleport(origin, new QAngle(IntPtr.Zero), new Vector(IntPtr.Zero));
        }

        /// <summary>
        /// Attaches a child entity to a specified parent entity with an optional offset and rotation.
        /// </summary>
        /// <param name="childEntity">The child entity to set the parent for.</param>
        /// <param name="parentEntity">The parent entity to set.</param>
        /// <param name="offset">Optional offset from the parent's origin.</param>
        /// <param name="rotation">Optional rotation relative to the parent's rotation.</param>
        public static void SetParent(this CBaseEntity childEntity, CBaseEntity parentEntity, Vector offset = null, QAngle rotation = null)
        {
            if (!childEntity.IsValid || !parentEntity.IsValid) return;

            offset = offset == null ? new Vector(IntPtr.Zero) : parentEntity.AbsOrigin.Clone().Add(x: offset.X, y: offset.Y, z: offset.Z);
            rotation ??= new QAngle(IntPtr.Zero);

            childEntity.Teleport(offset, rotation, new Vector(IntPtr.Zero));
            childEntity.SetParent(parentEntity);
        }

        /// <summary>
        /// Sets the health of the player controller's pawn.
        /// </summary>
        /// <param name="controller">The player controller.</param>
        /// <param name="health">The health value to set. Defaults to 100.</param>
        public static void SetHp(this CCSPlayerController controller, int health = 100)
        {
            var pawn = controller.PlayerPawn.Value;
            if (!controller.PawnIsAlive || pawn == null) return;

            pawn.Health = health;

            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }

        /// <summary>
        /// Sets the armor value of the player controller's pawn.
        /// </summary>
        /// <param name="controller">The player controller.</param>
        /// <param name="armor">The armor value to set. Defaults to 100.</param>
        public static void SetArmor(this CCSPlayerController controller, int armor = 100)
        {
            if (armor < 0 || !controller.PawnIsAlive || controller.PlayerPawn.Value == null) return;

            controller.PlayerPawn.Value.ArmorValue = armor;

            Utilities.SetStateChanged(controller.PlayerPawn.Value, "CCSPlayerPawn", "m_ArmorValue");
        }

        /// <summary>
        /// Sets the color of the entity.
        /// </summary>
        /// <param name="entity">The entity to set the color for.</param>
        /// <param name="color">The color to set.</param>
        public static void SetColor(this CBaseModelEntity entity, Color color)
        {
            if (entity == null || !entity.IsValid) return;

            entity.Render = color;
            Utilities.SetStateChanged(entity, "CBaseModelEntity", "m_clrRender");
        }

        /// <summary>
        /// Checks if the player is an ally of another player based on their team numbers.
        /// </summary>
        /// <param name="player">The player controller.</param>
        /// <param name="otherPlayer">The other player controller to compare against.</param>
        /// <returns>True if the player is an ally of the other player, otherwise false.</returns>
        public static bool AllyOf(this CCSPlayerController player, CCSPlayerController otherPlayer)
        {
            return player.PlayerPawn.Value.TeamNum == otherPlayer.PlayerPawn.Value.TeamNum;
        }

        /// <summary>
        /// Sets the scale of the skeleton instance of the entity's body component.
        /// </summary>
        /// <param name="entity">The entity whose skeleton instance scale is to be set.</param>
        /// <param name="scale">The scale value to set.</param>
        public static void SetScale(this CBaseModelEntity entity, float scale)
        {
            if (entity == null || !entity.IsValid) return;

            var skeletonInstance = entity.CBodyComponent.SceneNode.GetSkeletonInstance();
            if (skeletonInstance != null)
            {
                entity.AcceptInput("SetScale", null, null, scale.ToString());
                skeletonInstance.Scale = scale;
                Server.NextFrame(() =>
                {
                    Utilities.SetStateChanged(entity, "CBaseEntity", "m_CBodyComponent");
                });
            }
        }

        /// <summary>
        /// Plays a local sound for the player.
        /// </summary>
        /// <param name="player">The player controller.</param>
        /// <param name="soundPath">The path to the sound file.</param>
        public static void PlayLocalSound(this CCSPlayerController player, string soundPath)
        {
            if (player == null || !player.IsValid) return;

            player.ExecuteClientCommand($"play {soundPath}");
        }

        /// <summary>
        /// Applies an adrenaline surge effect to the player, boosting their health for a specified duration.
        /// </summary>
        /// <param name="player">The player controller to apply the effect to.</param>
        /// <param name="duration">The duration of the adrenaline surge effect in seconds. Default is 5 seconds.</param>
        public static void AdrenalineSurgeEffect(this CCSPlayerController player, float duration = 5f)
        {
            if (!player.IsAlive()) return;

            player.PlayerPawn.Value.HealthShotBoostExpirationTime = Server.CurrentTime + duration;
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
        }

        /// <summary>
        /// Calculates the velocity vector away from the player based on the given speed.
        /// </summary>
        /// <param name="player">The player controller.</param>
        /// <param name="speed">The speed of the velocity.</param>
        /// <returns>Returns a Vector representing the velocity away from the player.</returns>
        public static Vector CalculateVelocityAwayFromPlayer(this CCSPlayerController player, int speed)
        {
            var pawn = player.PlayerPawn.Value;
            float yawAngleRadians = (float)(pawn.EyeAngles.Y * Math.PI / 180.0);
            float yawCos = (float)(Math.Cos(yawAngleRadians) * speed);
            float yawSin = (float)(Math.Sin(yawAngleRadians) * speed);

            float pitchAngleRadians = (float)(pawn.EyeAngles.X * Math.PI / 180.0);
            float pitchSin = (float)(Math.Sin(pitchAngleRadians) * -speed);

            var velocity = new Vector(yawCos, yawSin, pitchSin);
            return velocity;
        }

        /// <summary>
        /// Calculates the position in front of the player based on the given distance, height, and horizontal offset.
        /// </summary>
        /// <param name="player">The player controller.</param>
        /// <param name="distance">The distance in front of the player.</param>
        /// <param name="height">The height offset from the player's position.</param>
        /// <param name="horizontalOffset">The horizontal offset from the player's position.</param>
        /// <returns>Returns a Vector representing the position in front of the player.</returns>
        public static Vector CalculatePositionInFront(this CCSPlayerController player, float distance, float height, float horizontalOffset = 0)
        {
            return player.PlayerPawn.Value.CalculatePositionInFront(new Vector(distance, horizontalOffset, height));
        }

        /// <summary>
        /// Calculates the position in front of the player based on the given offset.
        /// </summary>
        /// <param name="player">The player controller.</param>
        /// <param name="offset">The offset vector.</param>
        /// <returns>Returns a Vector representing the position in front of the player.</returns>
        public static Vector CalculatePositionInFront(this CCSPlayerController player, Vector offset)
        {
            var pawn = player.PlayerPawn.Value ?? throw new InvalidOperationException("PlayerPawn is not set.");
            float yawAngle = pawn.EyeAngles.Y;
            return player.PlayerPawn.Value.CalculatePositionInFront(offset, yawAngle);
        }

        /// <summary>
        /// Calculates the position in front of the entity based on the given offset and yaw angle.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="offset">The offset vector.</param>
        /// <param name="yawAngle">The yaw angle. If null, the entity's current yaw angle is used.</param>
        /// <returns>Returns a Vector representing the position in front of the entity.</returns>
        public static Vector CalculatePositionInFront(this CBaseModelEntity entity, Vector offset, float? yawAngle = null)
        {
            yawAngle ??= entity.AbsRotation.Y;
            float yawAngleRadians = (float)(yawAngle * Math.PI / 180.0);

            float offsetX = offset.X * MathF.Cos(yawAngleRadians) - offset.Y * MathF.Sin(yawAngleRadians);
            float offsetY = offset.X * MathF.Sin(yawAngleRadians) + offset.Y * MathF.Cos(yawAngleRadians);

            var positionInFront = new Vector
            {
                X = entity.AbsOrigin.X + offsetX,
                Y = entity.AbsOrigin.Y + offsetY,
                Z = entity.AbsOrigin.Z + offset.Z
            };

            return positionInFront;
        }

        /// <summary>
        /// Calculates the travel velocity vector from position A to position B over the given time duration.
        /// </summary>
        /// <param name="positionA">The starting position.</param>
        /// <param name="positionB">The ending position.</param>
        /// <param name="timeDuration">The time duration for the travel.</param>
        /// <returns>Returns a Vector representing the travel velocity.</returns>
        public static Vector CalculateTravelVelocity(Vector positionA, Vector positionB, float timeDuration)
        {
            Vector directionVector = positionB - positionA;
            float distance = directionVector.Length();

            if (timeDuration == 0)
            {
                timeDuration = 1;
            }

            float velocityMagnitude = distance / timeDuration;

            if (distance != 0)
            {
                directionVector /= distance;
            }

            Vector velocityVector = directionVector * velocityMagnitude;

            return velocityVector;
        }

        /// <summary>
        /// Checks if the player is behind another player based on their eye angles.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="anotherPlayer">The player to compare against.</param>
        /// <returns>True if the player is behind another given player, otherwise false.</returns>
        public static bool IsBehind(this CCSPlayerController player, CCSPlayerController anotherPlayer)
        {
            var behindAngle = player.PlayerPawn.Value.EyeAngles.Y;
            var infrontAngle = anotherPlayer.PlayerPawn.Value.EyeAngles.Y;

            return Math.Abs(behindAngle - infrontAngle) <= 50;
        }

        /// <summary>
        /// Disables the movement of the player.
        /// </summary>
        /// <param name="player">The player controller whose movement is to be disabled.</param>
        public static void DisableMovement(this CCSPlayerController player)
        {
            if (!player.IsAlive()) return;
            player.PlayerPawn.Value!.MoveType = MoveType_t.MOVETYPE_NONE;
            Schema.SetSchemaValue(player.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 0);
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
        }

        /// <summary>
        /// Enables the movement of the player.
        /// </summary>
        /// <param name="player">The player controller whose movement is to be enabled.</param>
        public static void EnableMovement(this CCSPlayerController player)
        {
            if (!player.IsAlive()) return;
            player.PlayerPawn.Value!.MoveType = MoveType_t.MOVETYPE_WALK;
            Schema.SetSchemaValue(player.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 2);
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
        }

        /// <summary>
        /// Blinds the player by displaying a fade effect with the specified duration and color.
        /// </summary>
        /// <param name="player">The player controller to blind.</param>
        /// <param name="duration">The duration of the fade effect in seconds.</param>
        /// <param name="color">The color of the fade effect.</param>
        public static void Blind(this CCSPlayerController player, float duration, Color color)
        {
            if (player == null || !player.IsValid) return;

            var fadeMsg = UserMessage.FromPartialName("Fade");
            fadeMsg.SetInt("duration", Convert.ToInt32(duration * 512));
            fadeMsg.SetInt("hold_time", Convert.ToInt32(duration * 512));

            int flag = 0x0001 | 0x0010; // FADE_IN with PURGE
            fadeMsg.SetInt("flags", flag);
            fadeMsg.SetInt("color", color.R | color.G << 8 | color.B << 16 | color.A << 24);
            fadeMsg.Send(player);
        }

        /// <summary>
        /// Removes the blind effect from the player by displaying a fade-out effect.
        /// </summary>
        /// <param name="player">The player controller to unblind.</param>
        public static void Unblind(this CCSPlayerController player)
        {
            if (player == null || !player.IsValid) return;

            var fadeMsg = UserMessage.FromPartialName("Fade");
            fadeMsg.SetInt("duration", 0);
            fadeMsg.SetInt("hold_time", 0);
            fadeMsg.SetInt("flags", 0x0002); // FADE_OUT
            fadeMsg.SetInt("color", 0);
            fadeMsg.Send(player);
        }

        /// <summary>
        /// Inflicts damage to the player from an attacker, with an optional inflictor.
        /// </summary>
        /// <param name="victim">The player receiving the damage.</param>
        /// <param name="damage">The amount of damage to inflict.</param>
        /// <param name="attacker">The player causing the damage.</param>
        /// <param name="killFeedIcon">The icon to display in the kill feed. Attacker must be set</param>
        /// <param name="inflictor">The entity causing the damage, if different from the attacker.</param>
        /// <param name="penetrationKill">Indicates if the damage was caused by a penetration.</param>
        public static void TakeDamage(this CCSPlayerController victim, float damage, CCSPlayerController attacker, KillFeedIcon? killFeedIcon = null, CCSPlayerController inflictor = null, bool penetrationKill = false)
        {
            var size = Schema.GetClassSize("CTakeDamageInfo");
            var ptr = Marshal.AllocHGlobal(size);

            for (var i = 0; i < size; i++)
                Marshal.WriteByte(ptr, i, 0);

            var damageInfo = new CTakeDamageInfo(ptr);
            var attackerInfo = new Struct.CAttackerInfo(attacker);

            Marshal.StructureToPtr(attackerInfo, new IntPtr(ptr.ToInt64() + 0x98), false);

            Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hInflictor", inflictor?.Pawn?.Raw ?? attacker.Pawn.Raw);
            Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hAttacker", attacker.Pawn.Raw);

            damageInfo.Damage = damage;
            damageInfo.NumObjectsPenetrated = penetrationKill ? 1 : 0;

            if (!victim.IsAlive() || !victim.Pawn.IsValid) return;
            var attackerClass = attacker?.GetWarcraftPlayer()?.GetClass();
            if (attackerClass != null) attackerClass.LastHurtOther = Server.CurrentTime;
            if (killFeedIcon != null) attackerClass?.SetKillFeedIcon(killFeedIcon);

            VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Invoke(victim.Pawn.Value, damageInfo);
            Marshal.FreeHGlobal(ptr);
        }

        /// <summary>
        /// Heals the specified player by a given amount, optionally indicating the healer and ability name.
        /// Displays chat messages to both the healed player and the healer (if provided).
        /// </summary>
        /// <param name="player">The player to heal.</param>
        /// <param name="amount">The amount of health to restore.</param>
        /// <param name="abilityName">The name of the ability used to heal (optional).</param>
        /// <param name="healer">The player who performed the healing (optional).</param>
        /// <returns>The actual amount of health restored, capped by MaxHealth.</returns>
        public static int Heal(this CCSPlayerController player, int amount, string abilityName = null, CCSPlayerController healer = null)
        {
            if (amount <= 0 || !player.IsAlive()) return 0;

            var healAmount = Math.Min(amount, player.PlayerPawn.Value.MaxHealth - player.PlayerPawn.Value.Health);
            var playerCalculatedHealth = player.PlayerPawn.Value.Health + healAmount;
            player.SetHp(Math.Min(playerCalculatedHealth, player.PlayerPawn.Value.MaxHealth));

            var abilitySuffix = string.IsNullOrEmpty(abilityName) ? "" : $"{ChatColors.Gold} [{abilityName}]";

            if (healer != null && healer != player)
            {
                player.PrintToChat($" {ChatColors.Green}+{healAmount} HP from {ChatColors.Default}{healer.GetRealPlayerName()}" + abilitySuffix);
                if (healer.IsValid)
                    healer.PrintToChat($" {ChatColors.Green}Healed {ChatColors.Default}{player.GetRealPlayerName()} {ChatColors.Green}+{healAmount} HP" + abilitySuffix);
            }
            else
            {
                player.PrintToChat($" {ChatColors.Green}+{healAmount} HP" + abilitySuffix);
            }

            return healAmount;
        }

        /// <summary>
        /// Drops the weapon with the specified designer name from the player's inventory.
        /// </summary>
        /// <param name="player">The player dropping the weapon.</param>
        /// <param name="weaponName">The designer name of the weapon to drop.</param>
        public static void DropWeaponByDesignerName(this CCSPlayerController player, string weaponName)
        {
            var matchedWeapon = player.PlayerPawn.Value.WeaponServices.MyWeapons.ToList()
                .Where(x => x.Value.DesignerName == weaponName).FirstOrDefault();

            if (matchedWeapon != null && matchedWeapon.IsValid)
            {
                player.PlayerPawn.Value.WeaponServices.ActiveWeapon.Raw = matchedWeapon.Raw;
                player.DropActiveWeapon();
            }
        }

        /// <summary>
        /// Gets, drops, or removes the weapon in the specified gear slot from the player's inventory.
        /// </summary>
        /// <param name="player">The player whose weapon will be manipulated.</param>
        /// <param name="slot">The gear slot to target. If null, no specific slot is targeted.</param>
        /// <param name="action">The action to perform: "get", "drop", or "remove".</param>
        /// <returns>The CCSWeaponBase if action is "get", otherwise null.</returns>
        private static CCSWeaponBase HandleWeaponBySlot(this CCSPlayerController player, gear_slot_t? slot, string action)
        {
            var weaponServices = player.PlayerPawn.Value?.WeaponServices;
            if (weaponServices is null) return null;

            foreach (var weapon in weaponServices.MyWeapons)
            {
                if (weapon.IsValid && weapon.Value is { } w)
                {
                    var ccsWeapon = w.As<CCSWeaponBase>();
                    if (ccsWeapon?.IsValid == true && ccsWeapon.VData?.GearSlot == slot)
                    {
                        switch (action)
                        {
                            case "get":
                                return ccsWeapon;
                            case "drop":
                                weaponServices.ActiveWeapon.Raw = weapon.Raw;
                                player.DropActiveWeapon();
                                return ccsWeapon;
                            case "remove":
                                weaponServices.ActiveWeapon.Raw = weapon.Raw;
                                player.DropActiveWeapon();
                                ccsWeapon.RemoveIfValid();
                                return null;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Drops the weapon in the specified gear slot from the player's inventory.
        /// </summary>
        /// <param name="player">The player whose weapon will be dropped.</param>
        /// <param name="slot">The gear slot to drop the weapon from. If null, no specific slot is targeted.</param>
        public static void DropWeaponBySlot(this CCSPlayerController player, gear_slot_t? slot = null)
        {
            player.HandleWeaponBySlot(slot, "drop");
        }

        /// <summary>
        /// Removes the weapon in the specified gear slot from the player's inventory.
        /// </summary>
        /// <param name="player">The player whose weapon will have a weapon removed.</param>
        /// <param name="slot">The gear slot to remove the weapon from. If null, no specific slot is targeted.</param>
        public static void RemoveWeaponBySlot(this CCSPlayerController player, gear_slot_t? slot = null)
        {
            player.HandleWeaponBySlot(slot, "remove");
        }

        /// <summary>
        /// Gets the weapon in the specified gear slot from the player's inventory.
        /// </summary>
        /// <param name="player">The player whose weapon will be retrieved.</param>
        /// <param name="slot">The gear slot to get the weapon from. If null, no specific slot is targeted.</param>
        /// <returns>The CCSWeaponBase if found, otherwise null.</returns>
        public static CCSWeaponBase GetWeaponBySlot(this CCSPlayerController player, gear_slot_t? slot = null)
        {
            return player.HandleWeaponBySlot(slot, "get");
        }

        /// <summary>
        /// Removes the entity if it is valid.
        /// </summary>
        /// <param name="obj">The entity to remove.</param>
        public static void RemoveIfValid(this CBaseEntity obj)
        {
            if (obj != null && obj.IsValid)
            {
                obj?.Remove();
            }
        }

        /// <summary>
        /// Checks if the player controller is valid and the pawn is alive.
        /// </summary>
        /// <param name="player">The player controller to check.</param>
        /// <returns>True if the player controller is valid and the pawn is alive, otherwise false.</returns>
        public static bool IsAlive(this CCSPlayerController player)
        {
            return player != null && player.IsValid && player.PawnIsAlive;
        }

        /// <summary>
        /// Checks if the player controller and pawn is valid.
        /// </summary>
        /// <param name="player">The player controller to check.</param>
        /// <returns>True if the player controller and pawn is valid</returns>
        public static bool IsValid(this CCSPlayerController player)
        {
            return player != null && player.IsValid && player.PlayerPawn.IsValid;
        }

        public static string GetRealPlayerName(this CCSPlayerController player)
        {
            if (player == null || !player.IsValid) return string.Empty;
            var playerNameClean = Regex.Replace(player.PlayerName, @"\d+\s\[.*\]\s", "");
            return playerNameClean.Trim();
        }

        /// <summary>
        /// Adjusts the brightness of the color by a specified factor.
        /// </summary>
        /// <param name="color">The original color.</param>
        /// <param name="factor">The factor by which to adjust the brightness. 1 is the original color, less than 1 darkens the color, and greater than 1 brightens the color. Max factor is 2.</param>
        /// <returns>Returns the adjusted color.</returns>
        public static Color AdjustBrightness(this Color color, float factor)
        {
            // Ensure the factor is between 0 (black) and 1 (original color) or higher
            factor = Math.Clamp(factor, 0f, 2f); // Clamping to a sensible range for brightness adjustment

            // Calculate the new RGB values
            int r = (int)(color.R + (255 - color.R) * (factor - 1));
            int g = (int)(color.G + (255 - color.G) * (factor - 1));
            int b = (int)(color.B + (255 - color.B) * (factor - 1));

            // Ensure the values are within the 0-255 range
            r = Math.Clamp(r, 0, 255);
            g = Math.Clamp(g, 0, 255);
            b = Math.Clamp(b, 0, 255);

            return Color.FromArgb(color.A, r, g, b);
        }

        /// <summary>
        /// Converts a Color object to its hexadecimal string representation.
        /// </summary>
        /// <param name="color">The Color object to convert.</param>
        /// <returns>A string representing the hexadecimal value of the color.</returns>
        public static string ToHex(this Color color)
        {
            // Format the color into hex (including alpha if needed)
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// Rolls a dice to determine success based on a given chance.
        /// </summary>
        /// <param name="chanceOfSuccess">The chance of success (0-100).</param>
        /// <returns>True if the roll is successful, otherwise false.</returns>
        private static bool RollDice(int chanceOfSuccess)
        {
            var roll = Random.Shared.NextInt64(100);
            return roll >= 100 - chanceOfSuccess;
        }

        /// <summary>
        /// Rolls a dice to determine success based on ability level. The higher the level, the higher the chance of success.
        /// </summary>
        /// <param name="abilityLevel">The level of the ability.</param>
        /// <param name="maxLevelChance">Chance of success at maximum level (default is 100).</param>
        /// <returns>True if the roll is successful, otherwise false.</returns>
        public static bool RollDice(int abilityLevel, int maxLevelChance = 100)
        {
            var chanceInterval = maxLevelChance / WarcraftPlugin.MaxSkillLevel;
            return RollDice(chanceInterval * abilityLevel);
        }

        /// <summary>
        /// Creates a box around a specified point with given dimensions.
        /// </summary>
        /// <param name="point">The center point of the box.</param>
        /// <param name="sizeX">The size of the box along the X-axis.</param>
        /// <param name="sizeY">The size of the box along the Y-axis.</param>
        /// <param name="heightZ">The height of the box along the Z-axis.</param>
        /// <returns>A Box3d object representing the created box.</returns>
        public static Box3d CreateBoxAroundPoint(Vector point, double sizeX, double sizeY, double heightZ)
        {
            return Geometry.CreateBoxAroundPoint(point, sizeX, sizeY, heightZ);
        }

        /// <summary>
        /// Creates a sphere around a specified point with given radius and segment counts.
        /// </summary>
        /// <param name="point">The center point of the sphere.</param>
        /// <param name="radius">The radius of the sphere.</param>
        /// <param name="numLatitudeSegments">The number of latitude segments (default is 10).</param>
        /// <param name="numLongitudeSegments">The number of longitude segments (default is 10).</param>
        /// <returns>A list of Vector3d objects representing the points of the sphere.</returns>
        public static List<Vector3d> CreateSphereAroundPoint(Vector point, double radius, int numLatitudeSegments = 10, int numLongitudeSegments = 10)
        {
            return Geometry.CreateSphereAroundPoint(point, radius, numLatitudeSegments, numLongitudeSegments);
        }

        /// <summary>
        /// Displays a box with specified color, duration, and width.
        /// </summary>
        /// <param name="box">The Box3d object to display.</param>
        /// <param name="color">The color of the box (default is null).</param>
        /// <param name="duration">The duration for which the box will be visible (default is 5 seconds).</param>
        /// <param name="width">The width of the box lines (default is 0.1).</param>
        public static void Show(this Box3d box, Color? color = null, float duration = 5, float width = 0.1f)
        {
            Geometry.DrawVertices(box.ComputeVertices(), color, duration, width);
        }

        /// <summary>
        /// Displays a list of points with specified color, duration, and width.
        /// </summary>
        /// <param name="points">The list of Vector3d points to display.</param>
        /// <param name="color">The color of the points (default is null).</param>
        /// <param name="duration">The duration for which the points will be visible (default is 5 seconds).</param>
        /// <param name="width">The width of the points lines (default is 0.1).</param>
        public static void Show(this List<Vector3d> points, Color? color = null, float duration = 5, float width = 0.1f)
        {
            Geometry.DrawVertices(points, color, duration, width);
        }

        /// <summary>
        /// Gets the collision box of an entity.
        /// </summary>
        /// <param name="entity">The entity to get the collision box for.</param>
        /// <returns>A Box3d object representing the collision box of the entity.</returns>
        public static Box3d CollisionBox(this CBaseModelEntity entity)
        {
            return entity.Collision.ToBox(entity.AbsOrigin);
        }

        /// <summary>
        /// Checks if a box contains a specified vector.
        /// </summary>
        /// <param name="box">The Box3d object.</param>
        /// <param name="vector">The vector to check.</param>
        /// <returns>True if the box contains the vector, otherwise false.</returns>
        public static bool Contains(this Box3d box, Vector vector)
        {
            return box.Contains(vector.ToVector3d());
        }

        /// <summary>
        /// Sends out a ray from one point to another, and optionally draws the result.
        /// </summary>
        /// <param name="origin">The starting point of the ray.</param>
        /// <param name="endOrigin">The ending point of the ray.</param>
        /// <param name="drawResult">Whether to draw the result of the ray trace.</param>
        /// <returns>Returns a Vector representing the result of the ray trace.</returns>
        public static Vector RayTrace(Vector origin, Vector endOrigin, bool drawResult = false)
        {
            return RayTracer.Trace(origin, endOrigin, drawResult);
        }

        /// <summary>
        /// Sends out a ray from the player's position in the direction of their eye angles and optionally draws the result.
        /// </summary>
        /// <param name="player">The player controller.</param>
        /// <param name="drawResult">Whether to draw the result of the ray trace.</param>
        /// <returns>Returns a Vector representing the result of the ray trace.</returns>
        public static Vector RayTrace(this CCSPlayerController player, bool drawResult = false)
        {
            return RayTracer.Trace(player.EyePosition(), player.PlayerPawn.Value.EyeAngles, drawResult, true);
        }
    }
}
