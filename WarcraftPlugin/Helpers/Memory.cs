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
            ? @"\x48\xB8\x2A\x2A\x2A\x2A\x2A\x2A\x2A\x2A\x55\x48\x89\xE5\x41\x55\x41\x54\x49\x89\xFC\x53\x48\x89\xF3"
            : @"\x48\x8B\xC4\x48\x89\x58\x10\x48\x89\x70\x18\x55\x57\x41\x56\x48\x8D\xA8\x08\xFF\xFF\xFF"
        );

        internal static MemoryFunctionWithReturn<nint, nint, nint, uint, nint, uint, uint, byte> CSoundOpGameSystem_SetSoundEventParam_Windows =
            new("48 89 5C 24 08 48 89 6C 24 10 56 57 41 56 48 83 EC 40 48 8B B4 24 80 00 00 00");
        internal static MemoryFunctionWithReturn<int, int, nint, uint, nint, short, uint, nint> CSoundOpGameSystem_SetSoundEventParam_Linux =
            new("55 48 89 E5 41 57 41 56 49 89 F6 48 89 D6 41 55 41 89 CD");

        internal static MemoryFunctionVoid<CBaseEntity, CBaseEntity, CUtlStringToken, matrix3x4_t> CBaseEntity_SetParent = new(
            Environment.OSVersion.Platform == PlatformID.Unix
                ? @"\x48\x85\xF6\x74\x2A\x48\x8B\x47\x10\xF6\x40\x31\x02\x75\x2A\x48\x8B\x46\x10\xF6\x40\x31\x02\x75\x2A\xB8\x2A\x2A\x2A\x2A"
                : @"\x4D\x8B\xD9\x48\x85\xD2\x74\x2A"
        );

        internal static MemoryFunctionWithReturn<nint, nint, nint, nint, nint, nint, int, CSmokeGrenadeProjectile> CSmokeGrenadeProjectile_CreateFunc = new(
            Environment.OSVersion.Platform == PlatformID.Unix
                ? @"\x55\x4C\x89\xC1\x48\x89\xE5\x41\x57\x41\x56\x49\x89\xD6"
                : @"\x48\x89\x5C\x24\x2A\x48\x89\x6C\x24\x2A\x48\x89\x74\x24\x2A\x57\x41\x56\x41\x57\x48\x83\xEC\x50\x4C\x8B\xB4\x24"
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
