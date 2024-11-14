using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using System.Drawing;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CounterStrikeSharp.API.Modules.Memory;
using System.Runtime.InteropServices;
using System.Linq;

namespace WarcraftPlugin.Helpers
{
    public static class Utility
    {
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

        public static Vector ToCenterOrigin(this CCSPlayerController player)
        {
            var pawnOrigin = player.PlayerPawn.Value.AbsOrigin;
            return new Vector(pawnOrigin.X, pawnOrigin.Y, pawnOrigin.Z + 44);
        }

        public static CParticleSystem SpawnParticle(Vector pos, string effectName, float duration = 5)
        {
            CParticleSystem particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
            if (!particle.IsValid) return null;
            particle.EffectName = effectName;
            particle?.Teleport(pos, new QAngle(), new Vector());
            particle.StartActive = true;
            particle?.DispatchSpawn();

            WarcraftPlugin.Instance.AddTimer(duration, () => particle?.RemoveIfValid());

            return particle;
        }

        public static void SpawnExplosion(Vector pos, float damage, float radius, CCSPlayerController attacker = null)
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
        }

        public static CSmokeGrenadeProjectile SpawnSmoke(Vector pos, CCSPlayerPawn attacker, Color color)
        {
            //var smokeProjectile = Utilities.CreateEntityByName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
            var smokeProjectile = CSmokeGrenadeProjectile_CreateFunc.Invoke(
                        pos.Handle,
                        new Vector(0,0,0).Handle,
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

            /*var smokeEffect = Utilities.CreateEntityByName<CParticleSystem>("particle_smokegrenade");
            smokeEffect.Teleport(pos, new QAngle(), new Vector(0, 0, 0));
            smokeEffect.DispatchSpawn();*/
        }

        public static MemoryFunctionVoid<CBaseEntity, string, int, float, float> CBaseEntity_EmitSoundParamsFunc = new(
            Environment.OSVersion.Platform == PlatformID.Unix
            ? @"\x48\xB8\x2A\x2A\x2A\x2A\x2A\x2A\x2A\x2A\x55\x48\x89\xE5\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3"
            : @"\x48\x8B\xC4\x48\x89\x58\x10\x48\x89\x70\x18\x55\x57\x41\x56\x48\x8D\xA8\x08\xFF\xFF\xFF"
            );

        public static void EmitSound(this CBaseEntity entity, string soundpath, int pitch = 1, float volume = 1, float delay = 0)
        {
            CBaseEntity_EmitSoundParamsFunc?.Invoke(entity, soundpath, pitch, volume, delay);
        }

        public static MemoryFunctionVoid<CBaseEntity, CBaseEntity, CUtlStringToken, matrix3x4_t> CBaseEntity_SetParent = new(
        Environment.OSVersion.Platform == PlatformID.Unix
            ? @"\x48\x85\xF6\x74\x2A\x48\x8B\x47\x10\xF6\x40\x31\x02\x75\x2A\x48\x8B\x46\x10\xF6\x40\x31\x02\x75\x2A\xB8\x2A\x2A\x2A\x2A"
            : @"\x4D\x8B\xD9\x48\x85\xD2\x74\x2A"
        );

        public static void SetParent(this CBaseEntity childEntity, CBaseEntity parentEntity)
        {
            if (!childEntity.IsValid || !parentEntity.IsValid) return;

            var origin = new Vector(childEntity.AbsOrigin!.X, childEntity.AbsOrigin!.Y, childEntity.AbsOrigin!.Z);
            CBaseEntity_SetParent.Invoke(childEntity, parentEntity, null, null);
            // If not teleported, the childrenEntity will not follow the parentEntity correctly.
            childEntity.Teleport(origin, new QAngle(IntPtr.Zero), new Vector(IntPtr.Zero));
        }

        public static void SetParent(this CBaseEntity childEntity, CBaseEntity parentEntity, Vector offset = null, QAngle rotation = null)
        {
            if (!childEntity.IsValid || !parentEntity.IsValid) return;

            offset = offset == null ? new Vector(IntPtr.Zero) : parentEntity.AbsOrigin.With().Add(x: offset.X, y: offset.Y, z: offset.Z);
            rotation ??= new QAngle(IntPtr.Zero);

            childEntity.Teleport(offset, rotation, new Vector(IntPtr.Zero));
            childEntity.SetParent(parentEntity);
        }

        public static MemoryFunctionWithReturn<nint, nint, nint, nint, nint, nint, int, CSmokeGrenadeProjectile> CSmokeGrenadeProjectile_CreateFunc = new(
                Environment.OSVersion.Platform == PlatformID.Unix
                    ? @"\x55\x4C\x89\xC1\x48\x89\xE5\x41\x57\x41\x56\x49\x89\xD6"
                    : @"\x48\x89\x5C\x24\x2A\x48\x89\x6C\x24\x2A\x48\x89\x74\x24\x2A\x57\x41\x56\x41\x57\x48\x83\xEC\x50\x4C\x8B\xB4\x24"
        );

        public static void DoDamage(this CCSPlayerController player, int damage) //TODO: Merge with TakeDamage
        {
            var victimHealth = player.PlayerPawn.Value.Health - damage;
            player.SetHp(victimHealth);
        }

        public static void SetHp(this CCSPlayerController controller, int health = 100)
        {
            var pawn = controller.PlayerPawn.Value;
            if (!controller.PawnIsAlive || pawn == null) return;

            pawn.Health = health;

            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }

        public static void SetArmor(this CCSPlayerController controller, int armor = 100)
        {
            if (armor < 0 || !controller.PawnIsAlive || controller.PlayerPawn.Value == null) return;

            controller.PlayerPawn.Value.ArmorValue = armor;

            Utilities.SetStateChanged(controller.PlayerPawn.Value, "CCSPlayerPawn", "m_ArmorValue");
        }

        public static void SetColor(this CBaseModelEntity entity, Color color)
        {
            if (entity == null) return;

            entity.Render = color;
            Utilities.SetStateChanged(entity, "CBaseModelEntity", "m_clrRender");
        }

        public static void PlayLocalSound(this CCSPlayerController player, string soundPath)
        {
            if (player == null || !player.IsValid) return;

            player.ExecuteClientCommand($"play {soundPath}");
        }

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

        public static Vector CalculatePositionInFront(this CCSPlayerController player, Vector offset)
        {
            var pawn = player.PlayerPawn.Value ?? throw new InvalidOperationException("PlayerPawn is not set.");
            float yawAngle = pawn.EyeAngles.Y;
            return player.PlayerPawn.Value.CalculatePositionInFront(offset, yawAngle);
        }

        public static Vector CalculatePositionInFront(this CBaseModelEntity entity, Vector offset, float? yawAngle = null)
        {
            yawAngle ??= entity.AbsRotation.Y;
            // Extract yaw angle from player's rotation QAngle and convert to radians
            float yawAngleRadians = (float)(yawAngle * Math.PI / 180.0);

            // Calculate offsets in x and y directions
            float offsetX = offset.X * MathF.Cos(yawAngleRadians) - offset.Y * MathF.Sin(yawAngleRadians);
            float offsetY = offset.X * MathF.Sin(yawAngleRadians) + offset.Y * MathF.Cos(yawAngleRadians);

            // Calculate the new position in front of the player
            var positionInFront = new Vector
            {
                X = entity.AbsOrigin.X + offsetX,
                Y = entity.AbsOrigin.Y + offsetY,
                Z = entity.AbsOrigin.Z + offset.Z
            };

            return positionInFront;
        }

        public static Vector CalculateTravelVelocity(Vector positionA, Vector positionB, float timeDuration)
        {
            // Step 1: Determine direction from A to B
            Vector directionVector = positionB - positionA;

            // Step 2: Calculate distance between A and B
            float distance = directionVector.Length();

            // Step 3: Choose a desired time duration for the movement
            // Ensure that timeDuration is not zero to avoid division by zero
            if (timeDuration == 0)
            {
                timeDuration = 1;
            }

            // Step 4: Calculate velocity magnitude based on distance and time
            float velocityMagnitude = distance / timeDuration;

            // Step 5: Normalize direction vector
            if (distance != 0)
            {
                directionVector /= distance;
            }

            // Step 6: Scale direction vector by velocity magnitude to get velocity vector
            Vector velocityVector = directionVector * velocityMagnitude;

            return velocityVector;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct CAttackerInfo
        {
            public CAttackerInfo(CEntityInstance attacker)
            {
                NeedInit = false;
                IsWorld = true;
                Attacker = attacker.EntityHandle.Raw;
                if (attacker.DesignerName != "cs_player_controller") return;

                var controller = attacker.As<CCSPlayerController>();
                IsWorld = false;
                IsPawn = true;
                AttackerUserId = (ushort)(controller.UserId ?? 0xFFFF);
                TeamNum = controller.TeamNum;
                TeamChecked = controller.TeamNum;
            }

            [FieldOffset(0x0)] public bool NeedInit = true;
            [FieldOffset(0x1)] public bool IsPawn = false;
            [FieldOffset(0x2)] public bool IsWorld = false;

            [FieldOffset(0x4)]
            public UInt32 Attacker;

            [FieldOffset(0x8)]
            public ushort AttackerUserId;

            [FieldOffset(0x0C)] public int TeamChecked = -1;
            [FieldOffset(0x10)] public int TeamNum = -1;
        }

        public static void TakeDamage(this CCSPlayerController player, float damage, CCSPlayerController attacker, CCSPlayerController inflictor = null)
        {
            var size = Schema.GetClassSize("CTakeDamageInfo");
            var ptr = Marshal.AllocHGlobal(size);

            for (var i = 0; i < size; i++)
                Marshal.WriteByte(ptr, i, 0);

            var damageInfo = new CTakeDamageInfo(ptr);
            var attackerInfo = new CAttackerInfo(player);

            Marshal.StructureToPtr(attackerInfo, new IntPtr(ptr.ToInt64() + 0x80), false);

            Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hInflictor", inflictor?.Pawn?.Raw ?? attacker.Pawn.Raw);
            Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hAttacker", attacker.Pawn.Raw);

            damageInfo.Damage = damage;

            VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Invoke(player.Pawn.Value, damageInfo);
            Marshal.FreeHGlobal(ptr);
        }

        public static void DropWeaponByDesignerName(this CCSPlayerController player, string weaponName)
        {
            var matchedWeapon = player.PlayerPawn.Value.WeaponServices.MyWeapons
                .Where(x => x.Value.DesignerName == weaponName).FirstOrDefault();

            if (matchedWeapon != null && matchedWeapon.IsValid)
            {
                player.PlayerPawn.Value.WeaponServices.ActiveWeapon.Raw = matchedWeapon.Raw;
                player.DropActiveWeapon();
            }
        }

        public static bool IsPlayerInSpottedByMask(Span<uint> spottedByMask, int playerId)
        {
            int maskIndex = playerId >> 5; // Bitwise shift instead of division by 32
            int bitPosition = playerId & 31; // Bitwise AND instead of modulo 32

            // Combined single check with bitwise operation
            return (maskIndex < spottedByMask.Length) && ((spottedByMask[maskIndex] & (1u << bitPosition)) != 0);
        }

        public static void RemoveIfValid(this CBaseEntity obj)
        {
            if (obj != null && obj.IsValid)
            {
                obj?.Remove();
            }
        }

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


        public static string ToHex(this Color color)
        {
            // Format the color into hex (including alpha if needed)
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
