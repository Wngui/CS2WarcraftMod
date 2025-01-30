using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Events
{
    public class EventPlayerKilledOther : EventPlayerDeath
    {
        public EventPlayerKilledOther(nint pointer) : base(pointer)
        {
        }
    }
}
