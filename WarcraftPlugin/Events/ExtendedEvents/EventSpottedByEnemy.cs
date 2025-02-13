using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;

namespace WarcraftPlugin.Events.ExtendedEvents
{
    public class EventSpottedByEnemy : GameEvent, ICustomGameEvent
    {
        public EventSpottedByEnemy() : base(0)
        {
        }

        public EventSpottedByEnemy(nint pointer) : base(pointer)
        {
        }

        public EventSpottedByEnemy(string name, bool force) : base(name, force)
        {
        }

        public CCSPlayerController UserId { get; set; }
    }
}
