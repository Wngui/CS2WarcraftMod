using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Localization;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.lang;

namespace WarcraftPlugin.Items;

internal abstract class ShopItem
{
    protected virtual string Name { get; }
    protected virtual string Description { get; }

    internal static IStringLocalizer Localizer => WarcraftPlugin.Instance?.Localizer;
    internal string InternalName => Name.Replace(" ", "_").ToLowerInvariant();
    internal string LocalizedName =>
        Localizer != null && Localizer.Exists($"item.{InternalName}.name")
            ? Localizer[$"item.{InternalName}.name"]
            : Name;
    internal string LocalizedDescription =>
        Localizer != null && Localizer.Exists($"item.{InternalName}.description")
            ? Localizer[$"item.{InternalName}.description"]
            : Description;
    internal abstract int Price { get; }

    /// <summary>
    /// Indicates whether the item should be consumed instantly on purchase.
    /// Instant items are not stored in the player's inventory.
    /// </summary>
    internal virtual bool IsInstant => false;

    internal abstract void Apply(CCSPlayerController player);

    internal virtual void OnPlayerHurtOther(EventPlayerHurtOther @event) { }
}
