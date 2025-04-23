using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Menu;

namespace WarcraftPlugin.Core
{
    public static class InventoryManagement
    {
        public static readonly Dictionary<CCSPlayerController, List<IShopItem>> Inventories = [];

        public static bool HasItem(this CCSPlayerController player, Type item)
        {
            return Inventories.TryGetValue(player, out var inventory) && inventory.Any(existingItem => existingItem.GetType() == item);
        }

        public static void AddItem(this CCSPlayerController player, IShopItem item)
        {
            if (!Inventories.TryGetValue(player, out List<IShopItem> value))
            {
                Inventories[player] = [];
            }

            value.Add(item);
        }

        public static void RemoveItem(this CCSPlayerController player, Type item)
        {
            if (Inventories.TryGetValue(player, out var inventory))
            {
                inventory.RemoveAll(existingItem => existingItem.GetType() == item.GetType());
            }
        }

        public static void ClearInventory(this CCSPlayerController player)
        {
            if (Inventories.TryGetValue(player, out var inventory))
            {
                foreach (var item in inventory)
                {
                    item.ResetEffect(player);
                }
                Inventories.Remove(player);
            }
        }
    }

    public class ShopMenu
    {
        public ShopMenu()
        {
            WarcraftPlugin.Instance.AddCommandListener("say", OnPlayerChat);
        }

        private HookResult OnPlayerChat(CCSPlayerController? player, CommandInfo info)
        {
            var message = info.GetArg(1).ToLower();
            if (player == null) return HookResult.Continue;

            if (message == "shop" || message == "shopmenu")
            {
                OpenShopMenu(player);
                return HookResult.Handled;
            }

            return HookResult.Continue;
        }

        private static void OpenShopMenu(CCSPlayerController player)
        {
            //List<Menu.Menu> pages = [];

            //for (int i = 0; i < 4; i++)
            //{
            //var menu = MenuManagerExtra.CreateMenu($"Shop Page {i + 1}/4", 6);
            var menu = MenuManager.CreateMenu("Shop Menu", 6);
            //menu.Category = "Shop";

            foreach (var item in Shop.GetAllItems())
            {
                string itemName = $"{item.Name} - ${item.Cost}";

                menu.Add(itemName, null, (pl, opt) =>
                {
                    var money = pl.InGameMoneyServices;
                    if (money == null || !pl.IsValid()) return;

                    int currentMoney = money.Account;

                    if (pl.HasItem(item.GetType()))
                    {
                        pl.PrintToChat($" {ChatColors.Red}✖ You already own {item.Name}{(item.IsPersistent ? " permanently" : " this round")}.");
                        return;
                    }

                    if (Shop.ItemRaceRestrictions.TryGetValue(item.GetType(), out var blacklist)
                        && blacklist.Contains(player?.GetWarcraftPlayer()?.GetClass()?.InternalName))
                    {
                        player.PrintToChat($" {ChatColors.Red}✖ Your race ({pl.GetWarcraftPlayer().GetClass().DisplayName}) cannot use this item.");
                        return;
                    }

                    if (currentMoney < item.Cost)
                    {
                        pl.PrintToChat($" {ChatColors.Red}✖ Not enough money for {item.Name} (${item.Cost}).");
                        return;
                    }

                    if (!item.Apply(pl))
                    {
                        Console.WriteLine("Item failed to apply");
                        return;
                    }

                    money.Account -= item.Cost; //TODO warcraft helper function
                    Utilities.SetStateChanged(pl, "CCSPlayerController", "m_pInGameMoneyServices");

                    if (item.IsPersistent)
                    {
                        pl.AddItem(item);
                    }

                    pl.PlayLocalSound("sounds/common/talk.vsnd");
                    pl.PrintToChat($" {ChatColors.Green}✔ You bought {item.Name} for ${item.Cost}!");
                });
            }

            //pages.Add(menu);
            //}

            //MenuManagerExtra.OpenMainMenuExtra(player, pages);
            MenuManager.OpenMainMenu(player, menu);
        }
    }

    public interface IShopItem
    {
        string Name { get; }
        int Cost { get; }
        bool IsPersistent { get; }
        bool Apply(CCSPlayerController player); //apply should have base logic
        void ResetEffect(CCSPlayerController player); // make reset optional?
    }

    public static class Shop
    {
        public static List<IShopItem> GetAllItems() =>
        [
            new BootsOfSpeed(),
            new RingOfRegen(),
            new NecklaceOfImmunity(),
            new GrandExpTome(),
            new MassiveExpTome(),
            new GamblingExpTome(),
            new SmallExpTome(),
            new FeatherBoots(),
            new LongjumpBoots(),
            new CloakOfInvisibility(),
            new OrbOfSlow(),
            new FmjBullets(),
            new DisguiseKit(),
            new PeriaptOfHealth(),
            new GiftOfExp(),
            new ScrollOfResurrection(),
            new GlovesOfWarmth(),
            new MaskOfDeath(),
            new HelmOfExcellence(),
            new OrbOfReflection()
        ];

        public static readonly Dictionary<Type, HashSet<string>> ItemRaceRestrictions = new()
        {
            { typeof(BootsOfSpeed), new() { "undead_scourge", "laser_light_show" } },
            { typeof(RingOfRegen), new() { "undead_scourge" } },
            { typeof(NecklaceOfImmunity), new() { "undead_scourge" } },
            { typeof(FeatherBoots), new() { "undead_scourge" } },
            { typeof(LongjumpBoots), new() { "undead_scourge" } },
            { typeof(CloakOfInvisibility), new() { "undead_scourge" } },
            { typeof(OrbOfSlow), new() { "undead_scourge" } },
            { typeof(DisguiseKit), new() { "undead_scourge" } },
            { typeof(GlovesOfWarmth), new() { "undead_scourge" } },
            { typeof(MaskOfDeath), new() { "undead_scourge" } },
            { typeof(HelmOfExcellence), new() { "undead_scourge" } },
            { typeof(OrbOfReflection), new() { "undead_scourge" } },
            { typeof(FmjBullets), new() { "undead_scourge" } },
        };
    }

    public class PeriaptOfHealth : IShopItem
    {
        public string Name => "Periapt of Health";
        public int Cost => 2400;
        public bool IsPersistent => false;
        public bool Apply(CCSPlayerController player)
        {
            if (player.PlayerPawn?.Value == null) return false;
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            Warcraft.SetHp(player, player.PlayerPawn.Value.Health + 50);
            player.PrintToChat($" {ChatColors.Green}+50 Health granted.");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class BootsOfSpeed : IShopItem
    {
        public string Name => "Boots of Speed";
        public int Cost => 2600;
        public bool IsPersistent => false;

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null || player.PlayerPawn?.Value == null) return false;

            string race = wcPlayer.GetClass().InternalName;

            player.PlayerPawn.Value.VelocityModifier += 0.25f;
            player.PrintToChat($" {ChatColors.Green}✔ Speed Boots equipped! (+25% movement speed)");
            return true;
        }

        public void ResetEffect(CCSPlayerController player)
        {
            if (player.IsValid && player.PlayerPawn?.Value != null)
                player.PlayerPawn.Value.VelocityModifier = 1.0f;
        }
    }

    public class RingOfRegen : IShopItem
    {
        public string Name => "Ring of Regen";
        public int Cost => 3500;
        public bool IsPersistent => false;

        private readonly Dictionary<CCSPlayerController, Timer> regenTimers = new();

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null || !player.IsValid || player.PlayerPawn?.Value == null) return false;

            string race = wcPlayer.GetClass().InternalName;

            void RegenTick()
            {
                if (!player.IsValid || !player.IsAlive() || player.PlayerPawn?.Value == null) return;

                int currentHp = player.PlayerPawn.Value.Health;
                int maxHealth = Math.Max(player.PlayerPawn.Value.MaxHealth, 200);
                if (currentHp < maxHealth)
                {
                    player.PlayerPawn.Value.Health = Math.Min(currentHp + 2, maxHealth);
                    Server.NextFrame(() => Utilities.SetStateChanged(player.PlayerPawn.Value!, "CBaseEntity", "m_iHealth"));
                }

                regenTimers[player] = WarcraftPlugin.Instance.AddTimer(1.0f, RegenTick);
            }

            regenTimers[player] = WarcraftPlugin.Instance.AddTimer(1.0f, RegenTick);
            player.PrintToChat($"{ChatColors.Green}✔ Regeneration active! (+2 HP/sec)");
            return true;
        }

        public void ResetEffect(CCSPlayerController player)
        {
            if (regenTimers.TryGetValue(player, out var timer))
            {
                timer.Kill();
                regenTimers.Remove(player);
                player.PrintToChat($" {ChatColors.Red}✖ Ring of Regeneration faded away.");
            }
        }
    }

    public class NecklaceOfImmunity : IShopItem // DISABLED 
    {
        public string Name => "[Disabled-Item]";
        public int Cost => 2500;
        public bool IsPersistent => false;

        public bool Apply(CCSPlayerController player)
        {
            player.PrintToChat($" {ChatColors.Red}✖ Necklace of Immunity is currently disabled.");
            return false; // Disabling Necklace of Immunity for now
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;


            //wcPlayer.HasUltimateImmunity = true;
            player.PrintToChat($" {ChatColors.Green}✔ You are now immune to ultimates this round.");
            return true;
        }

        public void ResetEffect(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer != null)
            {
                //wcPlayer.HasUltimateImmunity = false;
                player.PrintToChat($" {ChatColors.Red}✖ Your ultimate immunity has worn off.");
            }
        }
    }

    public class ScrollOfResurrection : IShopItem
    {
        public string Name => "Scroll of Resurrection";
        public int Cost => 5000;
        public bool IsPersistent => true;

        public bool Apply(CCSPlayerController player)
        {
            player.PrintToChat($" {ChatColors.Gold}✔ Scroll of Resurrection purchased. It will trigger automatically on death!");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class GrandExpTome : IShopItem
    {
        public string Name => "Grand Exp Tome";
        public int Cost => 5000;
        public bool IsPersistent => false;
        private const int xpToGive = 300;

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            WarcraftPlugin.Instance.XpSystem.AddXp(player, xpToGive);

            int curXp = wcPlayer.currentXp;
            int maxXp = wcPlayer.amountToLevel;
            int level = wcPlayer.GetLevel();

            player.PrintToChat($" {ChatColors.Green}✔ You gained {xpToGive} XP from the Grand Tome of Experience!");
            player.PrintToChat($" {ChatColors.Default}📘 You are now Level {level} ({curXp}/{maxXp} XP)");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class MassiveExpTome : IShopItem
    {
        public string Name => "Massive Exp Tome";
        public int Cost => 10000;
        public bool IsPersistent => false;
        private const int xpToGive = 600;

        public bool Apply(CCSPlayerController player)
        {
            var plugin = WarcraftPlugin.Instance;
            plugin.XpSystem.AddXp(player, xpToGive);

            var wcPlayer = plugin.GetWcPlayer(player);
            int curXp = wcPlayer.currentXp;
            int maxXp = wcPlayer.amountToLevel;
            int level = wcPlayer.GetLevel();

            player.PrintToChat($"{ChatColors.Green}✔ You gained {xpToGive} XP from the Massive Tome of Experience!");
            player.PrintToChat($"{ChatColors.Default}📘 You are now Level {level} ({curXp}/{maxXp} XP)");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class GamblingExpTome : IShopItem
    {
        public string Name => "Gambling Exp Tome";
        public int Cost => 10000;
        public bool IsPersistent => false;
        private const int xpToGiveMin = 100;
        private const int xpToGiveMax = 900;

        public bool Apply(CCSPlayerController player)
        {
            var plugin = WarcraftPlugin.Instance;
            var wcPlayer = plugin.GetWcPlayer(player);
            if (wcPlayer == null) return false;

            int xpToGive = Random.Shared.Next(xpToGiveMin, xpToGiveMax + 1);

            int roll = Random.Shared.Next(1, 431);
            bool isGold = roll == 1;

            if (isGold)
            {
                xpToGive += 1000;
                foreach (var p in Utilities.GetPlayers())
                {
                    p.PrintToChat($" {ChatColors.Gold}✨ {player.PlayerName} rolled a GOLD CASE and gained +1000 bonus XP!");
                }
            }

            plugin.XpSystem.AddXp(player, xpToGive);

            int curXp = wcPlayer.currentXp;
            int maxXp = wcPlayer.amountToLevel;
            int level = wcPlayer.GetLevel();

            player.PrintToChat($" {ChatColors.Green}🎲 You gained {xpToGive} XP from the Gambling Tome of Experience!");
            player.PrintToChat($"{ChatColors.Default}📘 You are now Level {level} ({curXp}/{maxXp} XP)");

            if (isGold)
            {
                player.PrintToChat($"{ChatColors.Gold}💛 You wasted your knife luck on this purchase...");
            }

            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }


    public class SmallExpTome : IShopItem
    {
        public string Name => "Exp Tome";
        public int Cost => 1000;
        public bool IsPersistent => false;
        private const int xpToGive = 50;

        public bool Apply(CCSPlayerController player)
        {
            var plugin = WarcraftPlugin.Instance;
            plugin.XpSystem.AddXp(player, xpToGive);

            var wcPlayer = plugin.GetWcPlayer(player);
            int curXp = wcPlayer.currentXp;
            int maxXp = wcPlayer.amountToLevel;
            int level = wcPlayer.GetLevel();

            player.PrintToChat($" {ChatColors.Green}✔ You gained {xpToGive} XP from the Tome of Experience!");
            player.PrintToChat($"{ChatColors.Default}📘 You are now Level {level} ({curXp}/{maxXp} XP)");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class GiftOfExp : IShopItem
    {
        public string Name => "Gift of Experience";
        public int Cost => 4000;
        public bool IsPersistent => false;
        private const int xpToGive = 300;

        public bool Apply(CCSPlayerController player)
        {
            var plugin = WarcraftPlugin.Instance;

            var teammates = Utilities.GetPlayers()
                .Where(p => p.IsValid && p != player && !p.IsBot && p.TeamNum == player.TeamNum)
                .ToList();

            if (teammates.Count == 0)
            {
                player.PrintToChat($" {ChatColors.Red}✖ No teammates found to gift XP to.");
                return false;
            }

            var chosen = teammates[Random.Shared.Next(teammates.Count)];

            plugin.XpSystem.AddXp(chosen, xpToGive);

            var wcChosen = plugin.GetWcPlayer(chosen);
            int curXp = wcChosen.currentXp;
            int maxXp = wcChosen.amountToLevel;
            int level = wcChosen.GetLevel();

            player.PrintToChat($" {ChatColors.Green}✔ You gifted {xpToGive} XP to {chosen.PlayerName}!");
            chosen.PrintToChat($" {ChatColors.Gold}✨ {player.PlayerName} has gifted you {xpToGive} XP!");
            chosen.PrintToChat($" {ChatColors.Default}📘 You are now Level {level} ({curXp}/{maxXp} XP)");

            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class FeatherBoots : IShopItem
    {
        public string Name => "Feather Boots";
        public int Cost => 3100;
        public bool IsPersistent => false;

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            player.PlayerPawn.Value.GravityScale = 0.65f;
            player.PrintToChat($" {ChatColors.Green}✔ Feather Boots equipped! Gravity reduced.");
            return true;
        }

        public void ResetEffect(CCSPlayerController player)
        {
            if (player.PlayerPawn?.Value != null)
            {
                player.PlayerPawn.Value.GravityScale = 1.0f;
                player.PrintToChat($" {ChatColors.Default}✖ Feather Boots have worn off.");
            }
        }
    }

    public class LongjumpBoots : IShopItem
    {
        public string Name => "Longjump Boots";
        public int Cost => 4000;
        public bool IsPersistent => false;

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            player.PrintToChat($"{ChatColors.Green}✔ Longjump Boots equipped. Press jump to leap forward!");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class CloakOfInvisibility : IShopItem
    {
        public string Name => "Cloak of Invisibility";
        public int Cost => 1800;
        public bool IsPersistent => false;

        public static void Invisibility(CCSPlayerController player, float duration, int amount)
        {
            if (player?.PlayerPawn?.Value == null) return;

            var currentColor = player.PlayerPawn.Value.Render;
            var newColor = Color.FromArgb(
                Math.Clamp(170, 0, 255), // TO DO :Change 170 to correct level of invis
                currentColor.R,
                currentColor.G,
                currentColor.B
            );

            player.PlayerPawn.Value.SetColor(newColor);
        }

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            Invisibility(player, 999f, 150);
            player.PrintToChat($" {ChatColors.Green}✔ Cloak of Invisibility equipped.");
            return true;
        }

        public void ResetEffect(CCSPlayerController player)
        {
            Invisibility(player, 999f, 255);
        }
    }

    public class OrbOfSlow : IShopItem
    {
        public string Name => "Orb of Slow";
        public int Cost => 2800;
        public bool IsPersistent => false;

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            player.PrintToChat($" {ChatColors.Green}✔ Orb of Slow equipped! You now have a chance to slow enemies on hit.");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class DisguiseKit : IShopItem
    {
        public string Name => "Disguise";
        public int Cost => 1400;
        public bool IsPersistent => false;

        private readonly string ctModel = "characters/models/ctm_fbi/ctm_fbi_variantb.vmdl";
        private readonly string tModel = "characters/models/tm_leet/tm_leet_variantj.vmdl";

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            var model = player.TeamNum switch
            {
                2 => ctModel,
                3 => tModel,
                _ => null
            };

            if (model == null) return false;

            player.PlayerPawn.Value.SetModel(model);
            player.PrintToChat($" {ChatColors.Green}✔ You are now disguised as the enemy!");
            return true;
        }

        public void ResetEffect(CCSPlayerController player)
        {
            // Let the game naturally reset model on round end/death
        }
    }


    public class GlovesOfWarmth : IShopItem
    {
        public string Name => "Gloves of Warmth";
        public int Cost => 2800;
        public bool IsPersistent => false;
        private static readonly Dictionary<CCSPlayerController, Timer> GrenadeTimers = [];

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            player.GiveNamedItem("weapon_hegrenade");
            player.PrintToChat($"{ChatColors.Green}✔ Gloves of Warmth equipped!");

            StartRegenLoop(player);
            return true;
        }

        private static void StartRegenLoop(CCSPlayerController player)
        {
            if (GrenadeTimers.TryGetValue(player, out Timer value))
                value?.Kill();

            GrenadeTimers[player] = WarcraftPlugin.Instance.AddTimer(1.0f, () =>
            {
                if (!player.IsValid || player.PlayerPawn?.Value == null || !player.IsAlive())
                    return;

                var weapons = player.PlayerPawn.Value.WeaponServices?.MyWeapons;
                if (weapons == null) return;

                bool hasGrenade = weapons.Any(w =>
                    w.Value?.DesignerName.Contains("hegrenade") == true ||
                    w.Value?.DesignerName.Contains("flashbang") == true ||
                    w.Value?.DesignerName.Contains("decoy") == true ||
                    w.Value?.DesignerName.Contains("incgrenade") == true);

                if (!hasGrenade)
                {
                    var grenades = new[] {
                    "weapon_hegrenade",
                    "weapon_flashbang",
                    "weapon_decoy",
                    "weapon_incgrenade"
                };

                    string selected = grenades[Random.Shared.Next(grenades.Length)];
                    player.GiveNamedItem(selected);
                    player.PrintToChat($" {ChatColors.Green}🧤 Gloves of Warmth: You received a new {selected.Replace("weapon_", "").ToUpper()}!");
                }
                StartRegenLoop(player);
            });
        }

        public void ResetEffect(CCSPlayerController player)
        {
            if (GrenadeTimers.TryGetValue(player, out var timer))
            {
                timer.Kill();
                GrenadeTimers.Remove(player);
            }
        }
    }

    public class MaskOfDeath : IShopItem
    {
        public string Name => "Mask of Death";
        public int Cost => 1900;
        public bool IsPersistent => false;

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            player.PrintToChat($" {ChatColors.Green}✔ Mask of Death equipped. You may reveal enemies!");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class HelmOfExcellence : IShopItem
    {
        public string Name => "Helm of Excellence";
        public int Cost => 3000;
        public bool IsPersistent => false;

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            player.PrintToChat($" {ChatColors.Green}✔ Helm of Excellence equipped. Headshots hurt less!");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class OrbOfReflection : IShopItem
    {
        public string Name => "Orb of Reflection";
        public int Cost => 2800;
        public bool IsPersistent => false;

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            player.PrintToChat($" {ChatColors.Green}✔ Orb of Reflection equipped! Some damage will be returned to attackers.");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class FmjBullets : IShopItem
    {
        public string Name => "FMJ Bullets";
        public int Cost => 2800;
        public bool IsPersistent => false;

        public bool Apply(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            if (wcPlayer?.GetClass() == null) return false;

            player.PrintToChat($" {ChatColors.Green}✔ FMJ Bullets equipped! Bonus armor-piercing damage enabled.");
            return true;
        }

        public void ResetEffect(CCSPlayerController player) { }
    }

    public class ItemEvents
    {
        public static void Register(WarcraftPlugin plugin) //ideally a one-liner from warcraft to bootstrap shop system
        {
            //Rather than handling item logic here, why not keep it in the item class?
            plugin.RegisterEventHandler<EventPlayerHurt>((@event, info) =>
            {
                var attacker = @event.Attacker;
                var victim = @event.Userid;

                if (attacker == null || victim == null || attacker == victim)
                    return HookResult.Continue;

                // --- Orb of Slow
                if (InventoryManagement.Inventories.TryGetValue(attacker, out var attackerItems) &&
                    attackerItems.Any(item => item is OrbOfSlow))
                {
                    var pawn = victim.PlayerPawn?.Value;
                    if (pawn == null) return HookResult.Continue;

                    var originalSpeed = pawn.VelocityModifier;
                    pawn.VelocityModifier = originalSpeed / 2f;
                    pawn.SetColor(Color.BlueViolet);

                    plugin.AddTimer(3f, () =>
                    {
                        if (victim.IsValid && victim.PlayerPawn?.Value != null)
                        {
                            victim.PlayerPawn.Value.VelocityModifier = originalSpeed;
                            victim.PlayerPawn.Value.SetColor(Color.White);
                        }
                    });
                }

                // --- Mask of Death 
                if (attackerItems != null &&
                    attackerItems.Any(item => item is MaskOfDeath) &&
                    Random.Shared.Next(100) < 20)
                {
                    if (victim.PlayerPawn?.Value != null)
                    {
                        victim.PlayerPawn.Value.SetColor(Color.FromArgb(255, 255, 255, 255));
                        victim.PrintToChat($" {ChatColors.Red}✖ Your invisibility and immunity were stripped!");
                    }
                }

                // --- Helm of Excellence 
                if (@event.Hitgroup == (int)HitGroup.Head &&
                    InventoryManagement.Inventories.TryGetValue(victim, out var victimItems) &&
                    victimItems.Any(item => item is HelmOfExcellence))
                {
                    int dmg = @event.DmgHealth;
                    int reduced = (int)(dmg * 0.65f);

                    if (victim.PlayerPawn?.Value != null)
                    {
                        int currentHp = victim.PlayerPawn.Value.Health;
                        int newHp = currentHp + (dmg - reduced);
                        victim.PlayerPawn.Value.Health = newHp;

                        victim.PrintToCenter("🛡️ Helm of Excellence absorbed damage!");
                        Server.NextFrame(() => Utilities.SetStateChanged(victim.PlayerPawn.Value, "CBaseEntity", "m_iHealth"));
                    }
                }

                // --- Orb of Reflection 
                victimItems = null;
                InventoryManagement.Inventories.TryGetValue(victim, out victimItems);

                if (victimItems != null && victimItems.Any(item => item is OrbOfReflection) && attacker.IsValid && attacker.IsAlive() && attacker.PlayerPawn?.Value != null)
                {
                    int reflected = (int)(@event.DmgHealth * 0.25f);
                    if (reflected > 0)
                    {
                        attacker.PlayerPawn.Value.Health -= reflected;

                        Server.NextFrame(() =>
                        {
                            if (attacker.PlayerPawn?.Value != null)
                                Utilities.SetStateChanged(attacker.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
                        });

                        attacker.PrintToChat($" {ChatColors.Red}⚡ You were struck by reflected damage!");
                        victim.PrintToChat($" {ChatColors.Green}✔ Orb of Reflection struck your attacker for {reflected} damage!");
                    }
                }
                return HookResult.Continue;
            });

            plugin.RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
            {
                var player = @event.Userid;

                if (!player.IsValid || player.PlayerPawn?.Value == null) return HookResult.Continue;

                plugin.AddTimer(0.2f, () =>
                {
                    if (!player.IsValid || player.PlayerPawn?.Value == null) return;
                });

                return HookResult.Continue;
            });

            plugin.RegisterEventHandler<EventPlayerHurtOther>((@event, info) =>
            {
                var attacker = @event.Attacker;
                var victim = @event.Userid;

                if (attacker == null || victim == null || attacker == victim)
                    return HookResult.Continue;

                InventoryManagement.Inventories.TryGetValue(attacker, out var attackerItems);

                if (attackerItems != null && attackerItems.Any(item => item is FmjBullets))
                {
                    @event.AddBonusDamage(5);
                    attacker.PrintToCenter("You dealt 5 additional damage with FMJ bullets!");
                }
                return HookResult.Continue;
            });

            plugin.RegisterEventHandler<EventPlayerDeath>((@event, info) =>
            {
                var victim = @event.Userid;

                if (!victim.IsValid || victim.PlayerPawn?.Value == null)
                    return HookResult.Continue;

                if (victim.UserId.HasValue && victim.HasItem(typeof(ScrollOfResurrection)))
                {
                    var teammates = Utilities.GetPlayers()
                        .Where(p => p != victim && p.IsValid && p.IsAlive() && p.TeamNum == victim.TeamNum)
                        .ToList();

                    if (teammates.Count > 0)
                    {
                        var anchor = teammates[Random.Shared.Next(teammates.Count-1)];
                        var respawnLocation = anchor.PlayerPawn.Value.AbsOrigin;

                        victim.PrintToChat($"{ChatColors.Gold}⏳ Resurrection scroll activated! Respawning in 3 seconds...");

                        plugin.AddTimer(3.0f, () =>
                        {
                            if (!victim.IsValid || victim.IsAlive()) return;

                            victim.Respawn();

                            plugin.AddTimer(0.2f, () =>
                            {
                                if (victim.IsValid && victim.PlayerPawn?.Value != null)
                                {
                                    victim.PlayerPawn.Value.Teleport(respawnLocation);
                                    victim.PrintToChat($"{ChatColors.Green}✔ You have been resurrected!");
                                    victim.RemoveItem(typeof(ScrollOfResurrection));
                                }
                            });
                        });
                    }
                }

                victim.ClearInventory();

                return HookResult.Continue;
            });

            plugin.RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
            {
                var player = @event.Userid;
                if (!player.IsValid) return HookResult.Continue;

                InventoryManagement.Inventories.Remove(player);

                return HookResult.Continue;
            });

            plugin.RegisterEventHandler<EventRoundStart>((@event, info) =>
            {
                // Apply persistent items after short delay
                foreach (var (player, items) in InventoryManagement.Inventories)
                {
                    if (!player.IsValid() || player.PlayerPawn?.Value == null) continue;

                    plugin.AddTimer(0.2f, () =>
                    {
                        foreach (var item in items)
                            item.Apply(player);
                    });
                }
                return HookResult.Continue;
            });

            plugin.RegisterEventHandler<EventPlayerJump>((@event, info) =>
            {
                var player = @event.Userid;

                if (!player.IsValid || player.PlayerPawn?.Value == null)
                    return HookResult.Continue;

                if (InventoryManagement.Inventories.TryGetValue(player, out var items) &&
                    items.Any(i => i is LongjumpBoots))
                {
                    plugin.AddTimer(0.05f, () =>
                    {
                        if (!player.IsValid || player.PlayerPawn?.Value == null)
                            return;

                        var angle = player.PlayerPawn.Value.EyeAngles;
                        var forward = new Vector();
                        NativeAPI.AngleVectors(angle.Handle, forward.Handle, nint.Zero, nint.Zero);

                        if (forward.Z < 0.55f)
                            forward.Z = 0.55f;

                        forward *= 520;

                        var pawn = player.PlayerPawn.Value;
                        pawn.AbsVelocity.X = forward.X;
                        pawn.AbsVelocity.Y = forward.Y;
                        pawn.AbsVelocity.Z = forward.Z;
                    });

                    plugin.AddTimer(0.05f, () =>
                    {
                        var pawn = player.PlayerPawn.Value;
                        float originalGravity = pawn.GravityScale;
                        pawn.GravityScale = 0.7f;

                        plugin.AddTimer(5f, () =>
                        {
                            if (player.IsValid && player.PlayerPawn?.Value != null)
                            {
                                player.PlayerPawn.Value.GravityScale = originalGravity;
                            }
                        });
                    });
                }
                return HookResult.Continue;
            });

            WarcraftPlugin.Instance.RegisterEventHandler<EventRoundEnd>((@event, info) =>
            {
                foreach (var (player, items) in InventoryManagement.Inventories)
                {
                    foreach (var item in items)
                        item.ResetEffect(player);
                }

                return HookResult.Continue;
            });
        }

        public enum HitGroup //move to warcraft, or does csharp have?
        {
            Generic = 0,
            Head = 1,
            Chest = 2,
            Stomach = 3,
            LeftArm = 4,
            RightArm = 5,
            LeftLeg = 6,
            RightLeg = 7,
            Gear = 10
        }
    }
}