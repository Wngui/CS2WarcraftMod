using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Events.ExtendedEvents
{
    public class EventPlayerHurtOther(nint pointer) : EventPlayerHurt(pointer), ICustomGameEvent
    {
    }
}
