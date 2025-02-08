using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;

namespace WarcraftPlugin.Events
{
    public class EventSpottedEnemy : GameEvent, ICustomGameEvent
    {
        public EventSpottedEnemy() : base(0)
        {
        }

        public EventSpottedEnemy(nint pointer) : base(pointer)
        {
        }

        public EventSpottedEnemy(string name, bool force) : base(name, force)
        {
        }

        public CCSPlayerController UserId { get; set; }
    }
}
