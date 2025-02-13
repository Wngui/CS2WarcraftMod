using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Events.ExtendedEvents
{
    public class EventPlayerHurtOther(nint pointer) : EventPlayerHurt(pointer), ICustomGameEvent
    {
    }
}
