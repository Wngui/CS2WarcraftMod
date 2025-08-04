using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Runtime.InteropServices;

namespace WarcraftPlugin.Helpers
{
    internal static class Memory
    {
        internal static MemoryFunctionVoid<CBaseEntity, string, int, float, float> CBaseEntity_EmitSoundParamsFunc = new(
            Environment.OSVersion.Platform == PlatformID.Unix
            ? @"48 B8 01 00 00 00 FF FF FF FF 55"
            : @"48 89 5C 24 ? 48 89 74 24 ? 48 89 7C 24 ? 4C 89 74 24 ? 55 48 8D 6C 24 ? 48 81 EC ? ? ? ? 45 33 F6 48 C7 45"
        );

        public static MemoryFunctionWithReturn<nint, nint, nint, uint, nint, uint, uint, byte> CSoundOpGameSystem_SetSoundEventParam_Windows =
            new("48 89 5C 24 08 48 89 6C 24 10 56 57 41 56 48 83 EC 40 48 8B B4 24 80 00 00 00");
        public static MemoryFunctionWithReturn<int, int, nint, uint, nint, short, uint, nint> CSoundOpGameSystem_SetSoundEventParam_Linux =
            new("55 48 89 E5 41 57 49 89 F7 41 56 45 89 CE 41 55 49 89 CD");

        internal static MemoryFunctionVoid<CBaseEntity, CBaseEntity, CUtlStringToken, matrix3x4_t> CBaseEntity_SetParent = new(
            Environment.OSVersion.Platform == PlatformID.Unix
                ? @"\x48\x85\xF6\x74\x2A\x48\x8B\x47\x10\xF6\x40\x31\x02\x75\x2A\x48\x8B\x46\x10\xF6\x40\x31\x02\x75\x2A\xB8\x2A\x2A\x2A\x2A"
                : @"\x4D\x8B\xD9\x48\x85\xD2\x74\x2A"
        );

        internal static MemoryFunctionWithReturn<nint, nint, nint, nint, nint, nint, int, CSmokeGrenadeProjectile> CSmokeGrenadeProjectile_CreateFunc = new(
            Environment.OSVersion.Platform == PlatformID.Unix
                ? @"55 4C 89 C1 48 89 E5 41 57 49 89 FF 41 56 45 89 CE"
                : @"48 8B C4 48 89 58 ? 48 89 68 ? 48 89 70 ? 57 41 56 41 57 48 81 EC ? ? ? ? 48 8B B4 24 ? ? ? ? 4D 8B F8"
        );
    }

    internal class Struct
    {
        [StructLayout(LayoutKind.Explicit)]
        internal struct CAttackerInfo
        {
            internal CAttackerInfo(CEntityInstance attacker)
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

            [FieldOffset(0x0)] internal bool NeedInit = true;
            [FieldOffset(0x1)] internal bool IsPawn = false;
            [FieldOffset(0x2)] internal bool IsWorld = false;

            [FieldOffset(0x4)]
            internal UInt32 Attacker;

            [FieldOffset(0x8)]
            internal ushort AttackerUserId;

            [FieldOffset(0x0C)] internal int TeamChecked = -1;
            [FieldOffset(0x10)] internal int TeamNum = -1;
        }
    }
}
