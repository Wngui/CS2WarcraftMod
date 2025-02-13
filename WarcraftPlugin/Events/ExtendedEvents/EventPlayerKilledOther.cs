using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Events.ExtendedEvents
{
    public class EventPlayerKilledOther : EventPlayerDeath, ICustomGameEvent
    {
        public EventPlayerKilledOther(nint pointer) : base(pointer)
        {
        }
    }
}
