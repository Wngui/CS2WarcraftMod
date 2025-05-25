using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System;

namespace WarcraftPlugin.Helpers
{
    internal class VolumeFix
    {
        internal static void Load()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Memory.CSoundOpGameSystem_SetSoundEventParam_Linux.Hook(OnSetSoundEventParam, HookMode.Pre);
            }
            else
            {
                Memory.CSoundOpGameSystem_SetSoundEventParam_Windows.Hook(OnSetSoundEventParam, HookMode.Pre);
            }
        }

        internal static void Unload()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Memory.CSoundOpGameSystem_SetSoundEventParam_Linux.Unhook(OnSetSoundEventParam, HookMode.Pre);
            }
            else
            {
                Memory.CSoundOpGameSystem_SetSoundEventParam_Windows.Unhook(OnSetSoundEventParam, HookMode.Pre);
            }
        }

        internal static HookResult OnSetSoundEventParam(DynamicHook hook)
        {
            var hash = hook.GetParam<uint>(3);
            if (hash == 0x2D8464AF)
            {
                hook.SetParam(3, 0xBD6054E9);
            }
            return HookResult.Continue;
        }
    }
}
