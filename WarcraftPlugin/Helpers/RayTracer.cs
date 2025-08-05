using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace WarcraftPlugin.Helpers
{
    public static class RayTracer
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate bool TraceShapeDelegate(
            nint GameTraceManager,
            nint vecStart,
            nint vecEnd,
            nint skip,
            ulong mask,
            byte a6,
            GameTrace* pGameTrace
        );

        private static readonly nint TraceFunc = NativeAPI.FindSignature(Addresses.ServerPath, Environment.OSVersion.Platform == PlatformID.Unix
            ? "48 B8 ? ? ? ? ? ? ? ? 55 66 0F EF C0 48 89 E5 41 57 41 56 49 89 D6"
            : "4C 8B DC 49 89 5B ? 49 89 6B ? 49 89 73 ? 57 41 56 41 57 48 81 EC");

        private static readonly nint GameTraceManager = NativeAPI.FindSignature(Addresses.ServerPath, Environment.OSVersion.Platform == PlatformID.Unix
            ? "4C 8D 05 ? ? ? ? BB"
            : "48 8B 0D ? ? ? ? 0C");

        public static unsafe Vector Trace(Vector _origin, QAngle _viewangles, bool drawResult = false, bool fromPlayer = false)
        {
            var _forward = new Vector();

            // Get forward vector from view angles
            NativeAPI.AngleVectors(_viewangles.Handle, _forward.Handle, 0, 0);
            var _endOrigin = new Vector(_origin.X + _forward.X * 8192, _origin.Y + _forward.Y * 8192, _origin.Z + _forward.Z * 8192);

            var d = 50;

            if (fromPlayer)
            {
                _origin.X += _forward.X * d;
                _origin.Y += _forward.Y * d;
                _origin.Z += _forward.Z * d;
            }

            return Trace(_origin, _endOrigin, drawResult);
        }

        public static unsafe Vector Trace(Vector _origin, Vector _endOrigin, bool drawResult = false)
        {
            var _gameTraceManagerAddress = Address.GetAbsoluteAddress(GameTraceManager, 3, 7);

            var traceShape = Marshal.GetDelegateForFunctionPointer<TraceShapeDelegate>(TraceFunc);

            // Console.WriteLine($"==== TraceFunc {TraceFunc} | GameTraceManager {GameTraceManager} | _gameTraceManagerAddress {_gameTraceManagerAddress} | _traceShape {_traceShape}");

            var _trace = stackalloc GameTrace[1];

            ulong mask = 0x1C1003;
            // var mask = 0xFFFFFFFF;
            var result = traceShape(*(nint*)_gameTraceManagerAddress, _origin.Handle, _endOrigin.Handle, 0, mask, 4, _trace);

            //Console.WriteLine($"RESULT {result}");

            //Console.WriteLine($"StartPos: {_trace->StartPos}");
            //Console.WriteLine($"EndPos: {_trace->EndPos}");
            //Console.WriteLine($"HitEntity: {(uint)_trace->HitEntity}");
            //Console.WriteLine($"Fraction: {_trace->Fraction}");
            //Console.WriteLine($"AllSolid: {_trace->AllSolid}");
            //Console.WriteLine($"ViewAngles: {_viewangles}");

            var endPos = new Vector(_trace->EndPos.X, _trace->EndPos.Y, _trace->EndPos.Z);

            if (drawResult)
            {
                Color color = Color.FromName("Green");
                if (result)
                {
                    color = Color.FromName("Red");
                }

                Warcraft.DrawLaserBetween(_origin, endPos, color, 5);
            }

            if (result)
            {
                return endPos;
            }

            return null;
        }
    }

    internal static class Address
    {
        static unsafe internal nint GetAbsoluteAddress(nint addr, nint offset, int size)
        {
            if (addr == nint.Zero)
            {
                throw new Exception("Failed to find RayTrace signature.");
            }

            int code = *(int*)(addr + offset);
            return addr + code + size;
        }

        static internal nint GetCallAddress(nint a)
        {
            return GetAbsoluteAddress(a, 1, 5);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x35)]
    internal unsafe struct Ray
    {
        [FieldOffset(0)] internal Vector3 Start;
        [FieldOffset(0xC)] internal Vector3 End;
        [FieldOffset(0x18)] internal Vector3 Mins;
        [FieldOffset(0x24)] internal Vector3 Maxs;
        [FieldOffset(0x34)] internal byte UnkType;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x44)]
    internal unsafe struct TraceHitboxData
    {
        [FieldOffset(0x38)] internal int HitGroup;
        [FieldOffset(0x40)] internal int HitboxId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xB8)]
    internal unsafe struct GameTrace
    {
        [FieldOffset(0)] internal void* Surface;
        [FieldOffset(0x8)] internal void* HitEntity;
        [FieldOffset(0x10)] internal TraceHitboxData* HitboxData;
        [FieldOffset(0x50)] internal uint Contents;
        [FieldOffset(0x78)] internal Vector3 StartPos;
        [FieldOffset(0x84)] internal Vector3 EndPos;
        [FieldOffset(0x90)] internal Vector3 Normal;
        [FieldOffset(0x9C)] internal Vector3 Position;
        [FieldOffset(0xAC)] internal float Fraction;
        [FieldOffset(0xB6)] internal bool AllSolid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x3a)]
    internal unsafe struct TraceFilter
    {
        [FieldOffset(0)] internal void* Vtable;
        [FieldOffset(0x8)] internal ulong Mask;
        [FieldOffset(0x20)] internal fixed uint SkipHandles[4];
        [FieldOffset(0x30)] internal fixed ushort arrCollisions[2];
        [FieldOffset(0x34)] internal uint Unk1;
        [FieldOffset(0x38)] internal byte Unk2;
        [FieldOffset(0x39)] internal byte Unk3;
    }

    internal unsafe struct TraceFilterV2
    {
        internal ulong Mask;
        internal fixed ulong V1[2];
        internal fixed uint SkipHandles[4];
        internal fixed ushort arrCollisions[2];
        internal short V2;
        internal byte V3;
        internal byte V4;
        internal byte V5;
    }
}