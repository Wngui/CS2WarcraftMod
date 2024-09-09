using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;

namespace WarcraftPlugin.Events
{
    internal class EventSpottedPlayer : GameEvent
    {
        public EventSpottedPlayer() : base(0)
        {
        }

        public EventSpottedPlayer(nint pointer) : base(pointer)
        {
        }

        public EventSpottedPlayer(string name, bool force) : base(name, force)
        {
        }

        public CCSPlayerController UserId { get; set; }
    }
}
