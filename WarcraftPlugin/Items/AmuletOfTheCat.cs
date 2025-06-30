using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Core.Effects;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System;
using System.Collections.Generic;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Items;

internal class AmuletOfTheCat : ShopItem
{
    internal override string Name => "Amulet of the Cat";
    internal override string Description => "Silent Footsteps";
    internal override int Price => 4000;

    internal override void Apply(CCSPlayerController player)
    {
        new SilentFootstepsEffect(player).Start();
    }

    private class SilentFootstepsEffect(CCSPlayerController owner) : WarcraftEffect(owner)
    {
        // Tracks all players currently under the silent footstep effect
        private static readonly HashSet<IntPtr> _silentPlayers = [];

        public override void OnStart()
        {
            if (_silentPlayers.Count == 0)
            {
                Memory.CBaseEntity_EmitSoundParamsFunc.Hook(PreEmitSound, HookMode.Pre);
            }

            _silentPlayers.Add(owner.PlayerPawn.Value.Handle);
            owner.PrintToChat(" Silent footsteps activated.");
        }

        private static HookResult PreEmitSound(DynamicHook hook)
        {
            var entity = hook.GetParam<CBaseEntity>(0);
            var soundPath = hook.GetParam<string>(1);

            if (!string.IsNullOrEmpty(soundPath) &&
                soundPath.Contains("footstep", StringComparison.OrdinalIgnoreCase) &&
                _silentPlayers.Contains(entity.Handle))
            {
                return HookResult.Handled;
            }

            return HookResult.Continue;
        }

        public override void OnTick() { }

        public override void OnFinish()
        {
            _silentPlayers.Remove(owner.PlayerPawn.Value.Handle);

            if (_silentPlayers.Count == 0)
            {
                Memory.CBaseEntity_EmitSoundParamsFunc.Unhook(PreEmitSound, HookMode.Pre);
            }
        }
    }
}
