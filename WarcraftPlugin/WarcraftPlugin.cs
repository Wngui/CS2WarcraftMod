﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using WarcraftPlugin.Adverts;
using WarcraftPlugin.Core;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Core.Preload;
using WarcraftPlugin.Events;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Items;
using WarcraftPlugin.lang;
using WarcraftPlugin.Menu;
using WarcraftPlugin.Menu.WarcraftMenu;
using WarcraftPlugin.Models;

namespace WarcraftPlugin
{
    public class Config : BasePluginConfig
    {
        [JsonPropertyName("ConfigVersion")] public override int Version { get; set; } = 8;
        [JsonPropertyName("DeactivatedClasses")] public string[] DeactivatedClasses { get; set; } = [];
        [JsonPropertyName("ShowCommandAdverts")] public bool ShowCommandAdverts { get; set; } = true;
        [JsonPropertyName("DefaultClass")] public string DefaultClass { get; set; }
        [JsonPropertyName("DisableNamePrefix")] public bool DisableNamePrefix { get; set; } = false;
        [JsonPropertyName("XpPerKill")] public float XpPerKill { get; set; } = 10;
        [JsonPropertyName("XpHeadshotModifier")] public float XpHeadshotModifier { get; set; } = 0.15f;
        [JsonPropertyName("XpKnifeModifier")] public float XpKnifeModifier { get; set; } = 0.25f;
        [JsonPropertyName("XpPerRoundWin")] public int XpPerRoundWin { get; set; } = 30;
        [JsonPropertyName("EnableLevelDifferenceXp")] public bool EnableLevelDifferenceXp { get; set; } = true;
        [JsonPropertyName("MatchReset")] public bool MatchReset { get; set; } = false;
        [JsonPropertyName("TotalLevelRequired")]
        public Dictionary<string, int> TotalLevelRequired { get; set; } = new()
        {
            {"shadowblade", 50}, {"dwarf_engineer", 60}, {"death_weaver", 70},
            {"silent_assassin", 80}, {"hammerstorm", 90}, {"sacred_warrior", 100}
        };
        [JsonPropertyName("ItemOverrides")]
        public Dictionary<string, Dictionary<string, object>> ItemOverrides { get; set; } =
            Shop.Items.GetConfigurableProperties();
                
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
        public const int MaxUltimateLevel = 1;

        private readonly Dictionary<IntPtr, WarcraftPlayer> WarcraftPlayers = [];
        private EventSystem _eventSystem;
        internal XpSystem XpSystem;
        internal ClassManager classManager;
        public EffectManager EffectManager;
        internal CooldownManager CooldownManager;
        internal AdvertManager AdvertManager;
        private Database _database;
        private Timer _saveClientsTimer;

        public Config Config { get; set; } = null!;

        internal WarcraftPlayer GetWcPlayer(CCSPlayerController player)
        {
            if (!player.IsValid) return null;

            WarcraftPlayers.TryGetValue(player.Handle, out var wcPlayer);
            if (wcPlayer == null)
            {
                if (player.IsBot || player.ControllingBot)
                {
                    wcPlayer = CreateBotPlayer(player);
                }
                else
                {
                    wcPlayer = _database.LoadPlayerFromDatabase(player, XpSystem);
                }
                WarcraftPlayers[player.Handle] = wcPlayer;
            }

            return wcPlayer;
        }

        internal void SetWcPlayer(CCSPlayerController player, WarcraftPlayer wcPlayer)
        {
            WarcraftPlayers[player.Handle] = wcPlayer;
        }

        private WarcraftPlayer CreateBotPlayer(CCSPlayerController player)
        {
            var classes = classManager.GetAllClasses();
            var randomClass = classes[Random.Shared.Next(classes.Length)];

            var info = new ClassInformation
            {
                SteamId = 0,
                RaceName = randomClass.InternalName,
                CurrentXp = 0,
                CurrentLevel = 0,
                AmountToLevel = XpSystem.GetXpForLevel(0)
            };

            var wcPlayer = new WarcraftPlayer(player);
            wcPlayer.LoadClassInformation(info, XpSystem);
            XpSystem.AutoSpendSkillPoints(wcPlayer);
            return wcPlayer;
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

            if (!string.IsNullOrEmpty(Server.MapName) && !hotReload)
            {
                //Plugin loaded using 'css_plugins load', resources potentially not precached
                Server.PrintToChatAll($" {ChatColors.Green}Warcraft {ChatColors.Red}loaded after map start, {ChatColors.Orange}reload the map {ChatColors.Red}to avoid errors.");
            }

            Localizer = LocalizerMiddleware.Load(Localizer, ModuleDirectory);

            MenuAPI.Load(this);

            _instance ??= this;

            XpSystem = new XpSystem(this, Config, Localizer);
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

            List<string> ultimateAliases =
            [
                "ultimate", "ult",
                ..Localizer["command.ultimate"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            foreach (var alias in ultimateAliases)
                AddUniqueCommand(alias, "ultimate", UltimatePressed);

            List<string> classAliases =
            [
                "changerace", "changeclass", "race", "class", "rpg", "cr",
                ..Localizer["command.changeclass"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            foreach (var alias in classAliases)
                AddUniqueCommand(alias, "change class", (player, _) => ShowClassMenu(player));

            List<string> resetAliases =
            [
                "reset", "resetskills", "resetskillpoints",
                ..Localizer["command.reset"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            foreach (var alias in resetAliases)
                AddUniqueCommand(alias, "reset skills", CommandResetSkills);

            List<string> factoryResetAliases =
            [
                "factoryreset", "fullreset", "resetlevels",
                ..Localizer["command.factoryreset"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            foreach (var alias in factoryResetAliases)
                AddUniqueCommand(alias, "reset levels", CommandFactoryReset);

            List<string> addXpAliases =
                [
                "addxp", "givexp", "xpadd",
                ..Localizer["command.addxp"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            foreach (var alias in addXpAliases)
                AddUniqueCommand(alias, "addxp", CommandAddXp);

            List<string> skillsAliases =
            [
                "skills", "level",
                ..Localizer["command.skills"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            foreach (var alias in skillsAliases)
                AddUniqueCommand(alias, "skills", (player, _) => ShowSkillsMenu(player));

            List<string> shopAliases =
            [
                "shopmenu", "shop", "buymenu",
                ..Localizer["command.shopmenu"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            foreach (var alias in shopAliases)
                AddUniqueCommand(alias, "open item shop", (player, _) => ShowShopMenu(player));

            List<string> helpAliases =
            [
                "rpg_help", "commands", "wcs", "war3menu",
                ..Localizer["command.help"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ];
            foreach (var alias in helpAliases)
                AddUniqueCommand(alias, "list all commands", CommandHelp);

            RegisterListener<Listeners.OnClientConnect>(OnClientPutInServerHandler);
            RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);
            RegisterListener<Listeners.OnMapEnd>(OnMapEndHandler);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnectHandler);

            RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
            {
                //Models - kept here for backwards compatibility
                manifest.AddResource("models/props/de_dust/hr_dust/dust_crates/dust_crate_style_01_32x32x32.vmdl");
                manifest.AddResource("models/tools/bullet_hit_marker.vmdl");
                manifest.AddResource("models/generic/bust_02/bust_02_a.vmdl"); //destructable prop
                manifest.AddResource("models/weapons/w_muzzlefireshape.vmdl"); //fireball
                manifest.AddResource("models/anubis/structures/pillar02_base01.vmdl"); //spring trap
                manifest.AddResource("sounds/physics/body/body_medium_break3.vsnd");

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

            StartSaveClientsTimer();

            _eventSystem = new EventSystem(this, Config);
            _eventSystem.Initialize();

            VolumeFix.Load();

            _database.Initialize(ModuleDirectory);
        }

        private void AddUniqueCommand(string name, string description, CommandInfo.CommandCallback method)
        {
            if (!CommandDefinitions.Any(x => x.Name == name))
            {
                AddCommand(name, description, method);
            }
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

        private void ShowShopMenu(CCSPlayerController player)
        {
            var wcPlayer = GetWcPlayer(player);
            if (wcPlayer != null)
                ShopMenu.Show(wcPlayer);
        }

        [RequiresPermissions("@css/addxp")]
        private void CommandAddXp(CCSPlayerController admin, CommandInfo commandInfo)
        {
            var xpArg = commandInfo.ArgByIndex(1);
            if (string.IsNullOrWhiteSpace(xpArg) || !int.TryParse(xpArg, out var xpToAdd))
            {
                admin.PrintToChat("Missing XP amount. Correct usage: !addxp <amount> [target]");
                return;
            }

            var target = admin;

            var targetArg = commandInfo.ArgByIndex(2);
            if (!string.IsNullOrWhiteSpace(targetArg))
            {
                var resolvedTarget = GetTarget(commandInfo, 2);
                if (resolvedTarget == null) return;

                target = resolvedTarget;
            }

            commandInfo.ReplyToCommand(Localizer["xp.add", xpToAdd, target.PlayerName]);
            XpSystem.AddXp(target, xpToAdd);
        }

        //[RequiresPermissions("@css/setlevel")]
        //private void CommandSetLevel(CCSPlayerController admin, CommandInfo commandInfo)
        //{
        //    var levelArg = commandInfo.ArgByIndex(1);
        //    if (string.IsNullOrWhiteSpace(levelArg) || !int.TryParse(levelArg, out var level))
        //    {
        //        admin.PrintToChat("Missing level. Correct usage: !setlevel <level> [target]");
        //        return;
        //    }

        //    var target = admin;

        //    var targetArg = commandInfo.ArgByIndex(2);
        //    if (!string.IsNullOrWhiteSpace(targetArg))
        //    {
        //        var resolvedTarget = GetTarget(commandInfo, 2);
        //        if (resolvedTarget == null) return;

        //        target = resolvedTarget;
        //    }

        //    TODO set level
        //}

        private static CCSPlayerController GetTarget(CommandInfo command, int argIndex)
        {
            var matches = command.GetArgTargetResult(argIndex).ToList();
            var arg = command.GetArg(argIndex);

            switch (matches.Count)
            {
                case 0:
                    command.ReplyToCommand($"Target \"{arg}\" not found.");
                    return null;

                case 1:
                    return matches[0];

                default:
                    command.ReplyToCommand($"Multiple targets found for \"{arg}\".");
                    return null;
            }
        }

        private void CommandHelp(CCSPlayerController player, CommandInfo commandinfo)
        {
            player.PrintToChat($" {Localizer["command.help.description"]}");
        }

        private void CommandResetSkills(CCSPlayerController client, CommandInfo commandinfo)
        {
            var wcPlayer = GetWcPlayer(client);

            var abilityCount = wcPlayer.GetClass().Abilities.Count;
            for (int i = 0; i < abilityCount; i++)
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
            wcPlayer.currentLevel = 0;
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

            _database.SavePlayerToDatabase(player);
            SetWcPlayer(player, null);
        }

        private void StartSaveClientsTimer()
        {
            _saveClientsTimer?.Kill();
            _saveClientsTimer = AddTimer(60.0f, _database.SaveClients, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        }

        private void OnMapEndHandler()
        {
            EffectManager.DestroyAllEffects();
            if (Config.MatchReset)
            {
                _database.ResetClients();
            }
            else
            {
                _database.SaveClients();
            }
        }

        private void OnMapStartHandler(string mapName)
        {
            if (Config.MatchReset)
            {
                _database.ResetClients();
            }
            StartSaveClientsTimer();
        }

        private void OnClientPutInServerHandler(int slot, string name, string ipAddress)
        {
            var player = new CCSPlayerController(NativeAPI.GetEntityFromIndex(slot + 1));
            Console.WriteLine($"Put in server {player.Handle}");
            if (!player.IsValid) return;

            if (player.IsBot)
            {
                WarcraftPlayers[player.Handle] = CreateBotPlayer(player);
            }
            else
            {
                WarcraftPlayers[player.Handle] = _database.LoadPlayerFromDatabase(player, XpSystem);
            }

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
                client.PlayLocalSound("sounds/common/talk.vsnd");
            }
            else if (!warcraftPlayer.GetClass().IsAbilityReady(3))
            {
                client.PrintToCenter(" " + Localizer["ultimate.countdown", Math.Ceiling(warcraftPlayer.GetClass().AbilityCooldownRemaining(3))]);
                client.PlayLocalSound("sounds/common/talk.vsnd");
            }
            else
            {
                GetWcPlayer(client)?.GetClass()?.InvokeAbility(3);
            }
        }

        public void OnConfigParsed(Config config)
        {
            Config = config;
            Shop.Items.ApplyOverrides(config);
        }

        public override void Unload(bool hotReload)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                //Avoid getting stuck in old menu
                player.EnableMovement();
            }
            AdvertManager?.Cancel();
            _saveClientsTimer?.Kill();
            _database.SaveClients();
            _database.Dispose();
            VolumeFix.Unload();
            base.Unload(hotReload);
        }
    }
}