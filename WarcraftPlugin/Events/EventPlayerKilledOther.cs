using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Events
{
    internal class EventPlayerKilledOther : EventPlayerDeath
    {
        public EventPlayerKilledOther(nint pointer) : base(pointer)
        {
        }
    }
}
