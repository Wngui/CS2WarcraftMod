using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Events
{
    internal class EventPlayerHurtOther : EventPlayerHurt
    {
        public EventPlayerHurtOther(nint pointer) : base(pointer)
        {
        }
    }
}
