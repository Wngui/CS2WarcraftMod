using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using System;

namespace WarcraftPlugin.Models
{
    internal class GameAction
    {
        public Type EventType { get; set; }
        public Action<GameEvent> Handler { get; set; }
        public HookMode HookMode { get; set; }
    }
}
