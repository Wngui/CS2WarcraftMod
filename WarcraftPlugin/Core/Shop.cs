using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using WarcraftPlugin.Items;

namespace WarcraftPlugin.Core
{
    internal static class Shop
    {
        internal static readonly List<ShopItem> Items;

        static Shop()
        {
            Items =
            [
                new BootsOfSpeed(),
                new SockOfFeathers(),
                new RingOfRegeneration(),
                new MaskOfDeath(),
                new AmuletOfTheCat(),
                new DaggerOfVenom(),
                new MoneySiphonScepter(),
                new OrbOfFrost(),
                new TalismanOfEvasion(),
                new AmuletOfVitality(),
                new GlovesOfWrath(),
                new GlovesOfCloud(),
                new GlovesOfDazzle(),
                new TomeOfExperience(),
                new TomeOfGambling()
            ];
        }
    }

    internal static class ShopItemExtensions
    {
        internal static void ApplyOverrides(this List<ShopItem> items, Config config)
        {
            if (config?.ItemOverrides != null)
            {
                foreach (var item in items)
                {
                    if (config.ItemOverrides.TryGetValue(item.InternalName, out var overrides))
                    {
                        foreach (var overrideEntry in overrides)
                        {
                            var property = item.GetType().GetProperty(overrideEntry.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (property != null && property.CanRead && property.CanWrite && Attribute.IsDefined(property, typeof(Configurable)))
                            {
                                try
                                {
                                    var oldValue = property.GetValue(item);
                                    object newValue;

                                    if (overrideEntry.Value is JsonElement jsonElement)
                                    {
                                        newValue = JsonSerializer.Deserialize(jsonElement, property.PropertyType);
                                    }
                                    else
                                    {
                                        newValue = Convert.ChangeType(overrideEntry.Value, property.PropertyType);
                                    }

                                    if (!Equals(oldValue, newValue))
                                    {
                                        property.SetValue(item, newValue);
                                        Console.WriteLine($"[Shop] Updated property '{property.Name}' of item '{item.InternalName}' from '{oldValue}' to '{newValue}'.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[Shop] Failed to update property '{overrideEntry.Key}' of item '{item.InternalName}': {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
        }

        internal static Dictionary<string, Dictionary<string, object>> GetConfigurableProperties(this List<ShopItem> items)
        {
            return items.Where(i => !string.IsNullOrEmpty(i.InternalName))
                .ToDictionary(
                    i => i.InternalName,
                    i => i.GetType()
                          .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                          .Where(p => p.CanRead && p.CanWrite && Attribute.IsDefined(p, typeof(Configurable)))
                          .ToDictionary(
                              p => p.Name,
                              p =>
                              {
                                  var value = p.GetValue(i);
                                  if (value == null)
                                      return p.PropertyType.IsValueType ? Activator.CreateInstance(p.PropertyType) : null;

                                  return value;
                              }
                          )
                );
        }
    }
}
