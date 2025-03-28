﻿using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API.Modules.Admin;
using WarcraftPlugin.Adverts;
using System.Text.Json.Serialization;
using WarcraftPlugin.Events;
using WarcraftPlugin.Menu;
using WarcraftPlugin.Menu.WarcraftMenu;
using WarcraftPlugin.Core;
using WarcraftPlugin.Models;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Core.Preload;
using WarcraftPlugin.lang;

namespace WarcraftPlugin
{
    public class Config : BasePluginConfig
    {
        [JsonPropertyName("ConfigVersion")] public override int Version { get; set; } = 3;

        [JsonPropertyName("DeactivatedClasses")] public string[] DeactivatedClasses { get; set; } = [];
        [JsonPropertyName("ShowCommandAdverts")] public bool ShowCommandAdverts { get; set; } = true;
        [JsonPropertyName("DefaultClass")] public string DefaultClass { get; set; }
        [JsonPropertyName("DisableNamePrefix")] public bool DisableNamePrefix { get; set; } = false;
        [JsonPropertyName("XpPerKill")] public float XpPerKill { get; set; } = 40;
        [JsonPropertyName("XpHeadshotModifier")] public float XpHeadshotModifier { get; set; } = 0.15f;
        [JsonPropertyName("XpKnifeModifier")] public float XpKnifeModifier { get; set; } = 0.25f;
    }

    public static class WarcraftPlayerExtensions
    {
        public static WarcraftPlayer GetWarcraftPlayer(this CCSPlayerController player)
        {
            return WarcraftPlugin.Instance.GetWcPlayer(player);
        }
    }

    public class WarcraftPlugin : BasePlugin, IPluginConfig<Config>
    {
        private static WarcraftPlugin _instance;
        public static WarcraftPlugin Instance => _instance;

        public override string ModuleName => "Warcraft";
        public override string ModuleVersion => "DEVELOPMENT";

        public const int MaxLevel = 16;
        public const int MaxSkillLevel = 5;
        public const int maxUltimateLevel = 1;

        private readonly Dictionary<IntPtr, WarcraftPlayer> WarcraftPlayers = [];
        private EventSystem _eventSystem;
        internal XpSystem XpSystem;
        internal ClassManager classManager;
        internal EffectManager EffectManager;
        internal CooldownManager CooldownManager;
        internal AdvertManager AdvertManager;
        private Database _database;

        public Config Config { get; set; } = null!;

        internal WarcraftPlayer GetWcPlayer(CCSPlayerController player)
        {
            if (!player.IsValid || player.IsBot || player.ControllingBot) return null;

            WarcraftPlayers.TryGetValue(player.Handle, out var wcPlayer);
            if (wcPlayer == null)
            {
                wcPlayer = _database.LoadPlayerFromDatabase(player, XpSystem);
                WarcraftPlayers[player.Handle] = wcPlayer;
            }

            return wcPlayer;
        }

        internal void SetWcPlayer(CCSPlayerController player, WarcraftPlayer wcPlayer)
        {
            WarcraftPlayers[player.Handle] = wcPlayer;
        }

        internal static void RefreshPlayerName(CCSPlayerController player)
        {
            if (player == null || !player.IsValid) return;
            if (Instance.Config.DisableNamePrefix) return;

            var warcraftPlayer = Instance.GetWcPlayer(player);

            if (warcraftPlayer == null) return;

            var playerNameClean = player.GetRealPlayerName();
            var playerNameWithPrefix = $"{warcraftPlayer.GetLevel()} [{warcraftPlayer.GetClass().LocalizedDisplayName}] {playerNameClean}";

            player.PlayerName = playerNameWithPrefix;
            Utilities.SetStateChanged(player, "CBasePlayerController", "m_iszPlayerName");

            Instance.AddTimer(1, () =>
            {
                if (player == null || !player.IsValid) return;
                player.PlayerName = playerNameWithPrefix;
                Utilities.SetStateChanged(player, "CBasePlayerController", "m_iszPlayerName");
            });
        }

        public override void Load(bool hotReload)
        {
            base.Load(hotReload);

            Localizer = LocalizerMiddleware.Load(Localizer, ModuleDirectory);

            MenuAPI.Load(this, hotReload);

            _instance ??= this;

            XpSystem = new XpSystem(this);
            XpSystem.GenerateXpCurve(110, 1.07f, MaxLevel);

            _database = new Database();
            classManager = new ClassManager();
            classManager.Initialize(ModuleDirectory, Config);

            EffectManager = new EffectManager();
            EffectManager.Initialize();

            CooldownManager = new CooldownManager();
            CooldownManager.Initialize();

            if (Config.ShowCommandAdverts)
            {
                AdvertManager = new AdvertManager();
                AdvertManager.Initialize();
            }

            AddCommand("ultimate", "ultimate", UltimatePressed);
            AddCommand(Localizer["command.ultimate"], "ultimate", UltimatePressed);

            AddCommand("changerace", "change class", (player, _) => ShowClassMenu(player));
            AddCommand("changeclass", "change class", (player, _) => ShowClassMenu(player));
            AddCommand("race", "change class", (player, _) => ShowClassMenu(player));
            AddCommand("class", "change class", (player, _) => ShowClassMenu(player));
            AddCommand("rpg", "change class", (player, _) => ShowClassMenu(player));
            AddCommand("cr", "change class", (player, _) => ShowClassMenu(player));
            AddCommand(Localizer["command.changeclass"], "change class", (player, _) => ShowClassMenu(player));

            AddCommand("reset", "reset skills", CommandResetSkills);
            AddCommand(Localizer["command.reset"], "reset skills", CommandResetSkills);

            AddCommand("factoryreset", "reset levels", CommandFactoryReset);
            AddCommand(Localizer["command.factoryreset"], "reset levels", CommandFactoryReset);

            AddCommand("addxp", "addxp", CommandAddXp);
            AddCommand(Localizer["command.addxp"], "addxp", CommandAddXp);

            AddCommand("skills", "skills", (player, _) => ShowSkillsMenu(player));
            AddCommand("level", "skills", (player, _) => ShowSkillsMenu(player));
            AddCommand(Localizer["command.skills"], "skills", (player, _) => ShowSkillsMenu(player));

            AddCommand("rpg_help", "list all commands", CommandHelp);
            AddCommand("commands", "list all commands", CommandHelp);
            AddCommand("wcs", "list all commands", CommandHelp);
            AddCommand("war3menu", "list all commands", CommandHelp);
            AddCommand(Localizer["command.help"], "list all commands", CommandHelp);

            RegisterListener<Listeners.OnClientConnect>(OnClientPutInServerHandler);
            RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);
            RegisterListener<Listeners.OnMapEnd>(OnMapEndHandler);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnectHandler);

            RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
            {
                //Models
                manifest.AddResource("models/weapons/w_eq_beartrap_dropped.vmdl");
                manifest.AddResource("models/props/de_dust/hr_dust/dust_crates/dust_crate_style_01_32x32x32.vmdl");
                manifest.AddResource("models/tools/bullet_hit_marker.vmdl");
                manifest.AddResource("models/generic/bust_02/bust_02_a.vmdl"); //destructable prop
                manifest.AddResource("models/weapons/w_muzzlefireshape.vmdl"); //fireball
                manifest.AddResource("models/weapons/w_eq_bumpmine.vmdl"); //drone
                manifest.AddResource("models/anubis/structures/pillar02_base01.vmdl"); //spring trap

                //manifest.AddResource("models/weapons/w_eq_tablet_dropped.vmdl");
                //manifest.AddResource("models/weapons/w_eq_tablet.vmdl");
                //manifest.AddResource("models/generic/conveyor_control_panel_01/conveyor_control_screen_01.vmdl");
                //"models/props/crates/csgo_drop_crate_community_22.vmdl", shop???
                //sounds/ui/panorama/claim_gift_01.vsnd_c // shop sound??
                //sounds/physics/metal/playertag_pickup_01.vsnd_c //shop sound
                manifest.AddResource("sounds/physics/body/body_medium_break3.vsnd");

                //sounds/music/survival_review_victory.vsnd_c // cool track

                //preload class specific resources
                foreach (var resources in classManager.GetAllClasses().SelectMany(x => x.PreloadResources).ToList())
                {
                    manifest.AddResource(resources);
                }

                foreach (var p in Particles.Paths)
                {
                    manifest.AddResource(p);
                }

            });

            if (hotReload)
            {
                OnMapStartHandler(null);
            }

            _eventSystem = new EventSystem(this, Config);
            _eventSystem.Initialize();

            _database.Initialize(ModuleDirectory);
        }

        private void ShowSkillsMenu(CCSPlayerController player)
        {
            SkillsMenu.Show(GetWcPlayer(player));
        }

        private void ShowClassMenu(CCSPlayerController player)
        {
            var databaseClassInformation = _database.LoadClassInformationFromDatabase(player);
            ClassMenu.Show(player, databaseClassInformation);
        }

        [RequiresPermissions("@css/addxp")]
        private void CommandAddXp(CCSPlayerController client, CommandInfo commandinfo)
        {
            if (string.IsNullOrEmpty(commandinfo.ArgByIndex(1))) return;

            var xpToAdd = Convert.ToInt32(commandinfo.ArgByIndex(1));

            Console.WriteLine(Localizer["xp.add", xpToAdd, client.PlayerName]);
            XpSystem.AddXp(client, xpToAdd);
        }

        private void CommandHelp(CCSPlayerController player, CommandInfo commandinfo)
        {
            player.PrintToChat($" {Localizer["command.help.description"]}");
        }

        private void CommandResetSkills(CCSPlayerController client, CommandInfo commandinfo)
        {
            var wcPlayer = GetWcPlayer(client);

            for (int i = 0; i < 4; i++)
            {
                wcPlayer.SetAbilityLevel(i, 0);
            }

            if (XpSystem.GetFreeSkillPoints(wcPlayer) > 0)
            {
                SkillsMenu.Show(wcPlayer);
            }
        }

        private void CommandFactoryReset(CCSPlayerController client, CommandInfo commandInfo)
        {
            var wcPlayer = GetWcPlayer(client);
            wcPlayer.currentLevel = 1;
            wcPlayer.currentXp = 0;
            CommandResetSkills(client, commandInfo);
            client.PlayerPawn.Value.CommitSuicide(false, false);
        }

        private void OnClientDisconnectHandler(int slot)
        {
            var player = new CCSPlayerController(NativeAPI.GetEntityFromIndex(slot + 1));
            // No bots, invalid clients or non-existent clients.
            // TODO: If player controls a bot while disconnecting, progress is not saved
            if (!player.IsValid || player.IsBot || player.ControllingBot) return;
            SetWcPlayer(player, null);
            _database.SavePlayerToDatabase(player);
        }

        private void OnMapStartHandler(string mapName)
        {
            AddTimer(60.0f, _database.SaveClients, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

            Server.PrintToConsole("Map Load Warcraft\n");
        }

        private void OnMapEndHandler()
        {
            EffectManager.DestroyAllEffects();
            _database.SaveClients();
        }

        private void OnClientPutInServerHandler(int slot, string name, string ipAddress)
        {
            var player = new CCSPlayerController(NativeAPI.GetEntityFromIndex(slot + 1));
            Console.WriteLine($"Put in server {player.Handle}");
            // No bots, invalid clients or non-existent clients.
            if (!player.IsValid || player.IsBot) return;

            if (!_database.PlayerExistsInDatabase(player.SteamID))
            {
                _database.AddNewPlayerToDatabase(player);
            }

            WarcraftPlayers[player.Handle] = _database.LoadPlayerFromDatabase(player, XpSystem);

            Console.WriteLine("Player just connected: " + WarcraftPlayers[player.Handle]);
        }

        internal WarcraftPlayer ChangeClass(CCSPlayerController player, string classInternalName)
        {
            _database.SavePlayerToDatabase(player);

            // Dont do anything if were already that race.
            if (classInternalName == player.GetWarcraftPlayer().className) return player.GetWarcraftPlayer();

            player.GetWarcraftPlayer().GetClass().PlayerChangingToAnotherRace();
            player.GetWarcraftPlayer().className = classInternalName;

            _database.SaveCurrentClass(player);
            var warcraftClass = _database.LoadPlayerFromDatabase(player, XpSystem);

            return warcraftClass;
        }

        private void UltimatePressed(CCSPlayerController client, CommandInfo commandinfo)
        {
            var warcraftPlayer = client.GetWarcraftPlayer();
            if (warcraftPlayer.GetAbilityLevel(3) < 1)
            {
                client.PrintToCenter(" " + Localizer["no.ultimate"]);
                client.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
            }
            else if (!warcraftPlayer.GetClass().IsAbilityReady(3))
            {
                client.PrintToCenter(" " + Localizer["ultimate.countdown", Math.Ceiling(warcraftPlayer.GetClass().AbilityCooldownRemaining(3))]);
                client.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
            }
            else
            {
                GetWcPlayer(client)?.GetClass()?.InvokeAbility(3);
            }
        }

        public void OnConfigParsed(Config config)
        {
            Config = config;
        }

        public override void Unload(bool hotReload)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                //Avoid getting stuck in old menu
                player.EnableMovement();
            }
            base.Unload(hotReload);
        }
    }
}