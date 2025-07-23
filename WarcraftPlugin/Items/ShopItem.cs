using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Localization;
using System;
using System.Drawing;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.lang;

namespace WarcraftPlugin.Items;

internal abstract class ShopItem
{
    [Configurable]
    internal bool IsDisabled { get; set; } = false;

    protected virtual string Name { get; }
    protected virtual FormattableString Description { get; }

    internal static IStringLocalizer Localizer => WarcraftPlugin.Instance?.Localizer;
    internal string InternalName => Name.Replace(" ", "_").ToLowerInvariant();

    internal string LocalizedName =>
        Localizer != null && Localizer.Exists($"item.{InternalName}.name")
            ? Localizer[$"item.{InternalName}.name"]
            : Name;

    internal string LocalizedDescription =>
    Localizer != null && Localizer.Exists($"item.{InternalName}.description")
        ? Localizer[$"item.{InternalName}.description", Description.GetArguments()]
        : Description.ToString();

    [Configurable]
    internal abstract int Price { get; set; }

    /// <summary>
    /// Indicates whether the item should be consumed instantly on purchase.
    /// Instant items are not stored in the player's inventory.
    /// </summary>
    internal virtual bool IsInstant => false;

    /// <summary>
    /// The display color of the item.
    /// </summary>
    internal virtual Color Color { get; set; } = Color.White; // Default: white

    internal abstract void Apply(CCSPlayerController player);

    internal virtual void OnPlayerHurtOther(EventPlayerHurtOther @event) { }
}

[AttributeUsage(AttributeTargets.Property)]
public class Configurable : Attribute { }
