using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Core.Effects;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class AmuletOfTheCat : ShopItem
{
    protected override string Name => "Amulet of the Cat";
    protected override string Description => "Silent Footsteps";
    internal override int Price { get; set; } = 4000;
    internal override Color Color { get; set; } = Color.FromArgb(255, 192, 192, 192); // Silver for stealth/unique

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
                WarcraftPlugin.Instance.HookUserMessage(208, PreFootstepMessage, HookMode.Pre);
            }

            _silentPlayers.Add(owner.PlayerPawn.Value.Handle);
            owner.PrintToChat($" {Localizer["item.amulet_of_the_cat.activated"]}");
        }

        private static HookResult PreFootstepMessage(UserMessage um)
        {
            var entityIndex = um.ReadInt("source_entity_index");
            var player = Utilities.GetPlayers().FirstOrDefault(p =>
                p.PlayerPawn.Value?.Index == entityIndex);

            if (player == null)
                return HookResult.Continue;

            if (!_silentPlayers.Contains(player.PlayerPawn.Value.Handle))
                return HookResult.Continue;

            um.Recipients.Clear();
            return HookResult.Stop;
        }

        public override void OnTick() { }

        public override void OnFinish()
        {
            _silentPlayers.Remove(owner.PlayerPawn.Value.Handle);

            if (_silentPlayers.Count == 0)
            {
                WarcraftPlugin.Instance.UnhookUserMessage(208, PreFootstepMessage, HookMode.Pre);
            }
        }
    }
}
