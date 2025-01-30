using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Events
{
    public class EventPlayerHurtOther : EventPlayerHurt
    {
        public EventPlayerHurtOther(nint pointer) : base(pointer)
        {
        }
    }
}
