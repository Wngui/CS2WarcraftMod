using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Items;

internal abstract class ShopItem
{
    internal abstract string Name { get; }
    internal abstract string Description { get; }
    internal abstract int Price { get; }

    internal abstract void Apply(CCSPlayerController player);
}
