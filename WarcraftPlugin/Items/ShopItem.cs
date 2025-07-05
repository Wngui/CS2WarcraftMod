using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;

namespace WarcraftPlugin.Items;

internal abstract class ShopItem
{
    internal abstract string Name { get; }
    internal abstract string Description { get; }
    internal abstract int Price { get; }

    /// <summary>
    /// Indicates whether the item should be consumed instantly on purchase.
    /// Instant items are not stored in the player's inventory.
    /// </summary>
    internal virtual bool IsInstant => false;

    internal abstract void Apply(CCSPlayerController player);

    internal virtual void OnPlayerHurtOther(EventPlayerHurtOther @event) { }
}
