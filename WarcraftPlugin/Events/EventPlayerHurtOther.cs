using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Events
{
    public class EventPlayerHurtOther : EventPlayerHurt, ICustomGameEvent
    {
        public EventPlayerHurtOther(nint pointer) : base(pointer)
        {
        }
    }
}
