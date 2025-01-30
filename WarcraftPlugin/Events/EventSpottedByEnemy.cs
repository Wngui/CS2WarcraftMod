using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;

namespace WarcraftPlugin.Events
{
    public class EventSpottedByEnemy : GameEvent
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
