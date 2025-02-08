using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;

namespace WarcraftPlugin.Adverts
{
    internal class AdvertManager
    {
        private readonly float _interval = 180f;
        private int _advertIndex = 0;

        internal void Initialize()
        {
            WarcraftPlugin.Instance.AddTimer(_interval, AdvertTick, TimerFlags.REPEAT);
        }

        private void AdvertTick()
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (!player.IsValid || player.IsBot) continue;
                player.PrintToChat($" { ChatColors.Green}{ _adverts[_advertIndex]}");
            }

            _advertIndex++;

            if(_advertIndex >= _adverts.Count) _advertIndex = 0; 
        }

        private static readonly List<string> _adverts = [
            $"Want to try a new class? Type {ChatColors.Gold}!class{ChatColors.Green} to change",
            $"Unspent skill points? Type {ChatColors.Gold}!skills{ChatColors.Green} to level up abilities",
            $"Want to try new abilities? Type {ChatColors.Gold}!reset{ChatColors.Green} to reassign",
            $"Want to use your ultimate? Type 'bind <key> ultimate' in console. Example: {ChatColors.Grey}bind x ultimate"
        ];
    }
}
