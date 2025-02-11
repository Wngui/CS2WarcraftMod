using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Events
{
    public class EventPlayerHurtOther(nint pointer) : EventPlayerHurt(pointer), ICustomGameEvent
    {
    }
}
