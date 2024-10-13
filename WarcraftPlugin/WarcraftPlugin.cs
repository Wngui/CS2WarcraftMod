using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftPlugin.Cooldowns;
using WarcraftPlugin.Effects;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using System.Text.RegularExpressions;
using WarcraftPlugin.Resources;
using CounterStrikeSharp.API.Modules.Admin;
using WarcraftPlugin.Adverts;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Text.Json.Serialization;
using WarcraftPlugin.Events;
using WarcraftPlugin.Classes;
using WarcraftPlugin.Menu;
using System.Diagnostics;
using System.Drawing;
using CounterStrikeSharp.API.Modules.Entities;
using System.Numerics;

namespace WarcraftPlugin
{
    public class Config : BasePluginConfig
    {
        [JsonPropertyName("ConfigVersion")] public override int Version { get; set; } = 2;

        [JsonPropertyName("DeactivatedClasses")] public string[] DeactivatedClasses { get; set; } = [];
        [JsonPropertyName("ShowCommandAdverts")] public bool ShowCommandAdverts { get; set; } = false;
        [JsonPropertyName("NecromancerUseZombieModel")] public bool NecromancerUseZombieModel { get; set; } = true;
    }

    public static class WarcraftPlayerExtensions
    {
        public static WarcraftPlayer GetWarcraftPlayer(this CCSPlayerController player)
        {
            return WarcraftPlugin.Instance.GetWcPlayer(player);
        }
    }

    public class XpSystem
    {
        private readonly WarcraftPlugin _plugin;

        public XpSystem(WarcraftPlugin plugin)
        {
            _plugin = plugin;
        }

        private readonly List<int> _levelXpRequirement = new(new int[256]);

        public void GenerateXpCurve(int initial, float modifier, int maxLevel)
        {
            for (int i = 1; i <= maxLevel; i++)
            {
                if (i == 1)
                    _levelXpRequirement[i] = initial;
                else
                    _levelXpRequirement[i] = Convert.ToInt32(_levelXpRequirement[i - 1] * modifier);
            }
        }

        public int GetXpForLevel(int level)
        {
            return _levelXpRequirement[level];
        }

        public void AddXp(CCSPlayerController player, int xpToAdd)
        {
            var wcPlayer = _plugin.GetWcPlayer(player);
            if (wcPlayer == null) return;

            if (wcPlayer.GetLevel() >= WarcraftPlugin.MaxLevel) return;

            wcPlayer.currentXp += xpToAdd;

            while (wcPlayer.currentXp >= wcPlayer.amountToLevel)
            {
                wcPlayer.currentXp = wcPlayer.currentXp - wcPlayer.amountToLevel;
                GrantLevel(wcPlayer);

                if (wcPlayer.GetLevel() >= WarcraftPlugin.MaxLevel) return;
            }
        }

        public void GrantLevel(WarcraftPlayer wcPlayer)
        {
            if (wcPlayer.GetLevel() >= WarcraftPlugin.MaxLevel) return;

            wcPlayer.currentLevel += 1;

            RecalculateXpForLevel(wcPlayer);
            PerformLevelupEvents(wcPlayer);
        }

        private static void PerformLevelupEvents(WarcraftPlayer wcPlayer)
        {
            if (GetFreeSkillPoints(wcPlayer) > 0)
                WarcraftPlugin.ShowSkillPointMenu2(wcPlayer);

            wcPlayer.GetPlayer().PlayLocalSound("play sounds/ui/achievement_earned.vsnd");
            Utility.SpawnParticle(wcPlayer.GetPlayer().PlayerPawn.Value.AbsOrigin, "particles/ui/ammohealthcenter/ui_hud_kill_streaks_glow_5.vpcf", 1);
            WarcraftPlugin.RefreshPlayerName(wcPlayer);
        }

        public void RecalculateXpForLevel(WarcraftPlayer wcPlayer)
        {
            if (wcPlayer.currentLevel == WarcraftPlugin.MaxLevel)
            {
                wcPlayer.amountToLevel = 0;
                return;
            }

            wcPlayer.amountToLevel = GetXpForLevel(wcPlayer.currentLevel);
        }

        public static int GetFreeSkillPoints(WarcraftPlayer wcPlayer)
        {
            int totalPointsUsed = 0;

            for (int i = 0; i < 4; i++)
            {
                totalPointsUsed += wcPlayer.GetAbilityLevel(i);
            }

            int level = wcPlayer.GetLevel();
            if (level > WarcraftPlugin.MaxLevel)
                level = WarcraftPlugin.MaxSkillLevel;

            return level - totalPointsUsed;
        }
    }

    public class WarcraftPlayer
    {
        private int _playerIndex;
        public int Index => _playerIndex;
        public bool IsMaxLevel => currentLevel == WarcraftPlugin.MaxLevel;
        public CCSPlayerController GetPlayer() => Player;

        public CCSPlayerController Player { get; init; }

        public int currentXp;
        public int currentLevel;
        public int amountToLevel;
        public string className;
        public string statusMessage;

        private readonly List<int> _abilityLevels = new(new int[4]);
        public List<float> AbilityCooldowns = new(new float[4]);

        private WarcraftClass _class;

        public WarcraftPlayer(CCSPlayerController player)
        {
            Player = player;
        }

        public void LoadFromDatabase(DatabaseRaceInformation dbRace, XpSystem xpSystem)
        {
            currentLevel = dbRace.CurrentLevel;
            currentXp = dbRace.CurrentXp;
            className = dbRace.RaceName;
            amountToLevel = xpSystem.GetXpForLevel(currentLevel);

            _abilityLevels[0] = dbRace.Ability1Level;
            _abilityLevels[1] = dbRace.Ability2Level;
            _abilityLevels[2] = dbRace.Ability3Level;
            _abilityLevels[3] = dbRace.Ability4Level;

            _class = WarcraftPlugin.Instance.classManager.InstantiateClass(className);
            _class.WarcraftPlayer = this;
            _class.Player = Player;
        }

        public int GetLevel()
        {
            if (currentLevel > WarcraftPlugin.MaxLevel) return WarcraftPlugin.MaxLevel;

            return currentLevel;
        }

        public override string ToString()
        {
            return
                $"[{_playerIndex}]: {{raceName={className}, currentLevel={currentLevel}, currentXp={currentXp}, amountToLevel={amountToLevel}}}";
        }

        public int GetAbilityLevel(int abilityIndex)
        {
            return _abilityLevels[abilityIndex];
        }

        public static int GetMaxAbilityLevel(int abilityIndex)
        {
            return abilityIndex == 3 ? 1 : WarcraftPlugin.MaxSkillLevel;
        }

        public void SetAbilityLevel(int abilityIndex, int value)
        {
            _abilityLevels[abilityIndex] = value;
        }

        public WarcraftClass GetClass()
        {
            return _class;
        }

        public void SetStatusMessage(string status, float duration = 2f)
        {
            statusMessage = status;
            _ = new Timer(duration, () => statusMessage = null, 0);
            GetPlayer().PrintToChat(" " + status);
        }

        public void GrantAbilityLevel(int abilityIndex)
        {
            Player.PlayLocalSound("sounds/buttons/button9.vsnd");
            _abilityLevels[abilityIndex] += 1;
        }
    }

    public class WarcraftPlugin : BasePlugin, IPluginConfig<Config>
    {
        private static WarcraftPlugin _instance;
        public static WarcraftPlugin Instance => _instance;

        public override string ModuleName => "Warcraft";
        public override string ModuleVersion => "1.0.0";

        public const int MaxLevel = 16;
        public const int MaxSkillLevel = 5;
        public const int maxUltimateLevel = 1;

        private readonly Dictionary<IntPtr, WarcraftPlayer> WarcraftPlayers = [];
        private EventSystem _eventSystem;
        public XpSystem XpSystem;
        public RaceManager classManager;
        public EffectManager EffectManager;
        public CooldownManager CooldownManager;
        public AdvertManager AdvertManager;
        private Database _database;

        public int XpPerKill = 40;
        public float XpHeadshotModifier = 0.15f;
        public float XpKnifeModifier = 0.25f;

        public int BotQuota = 10;

        public List<WarcraftPlayer> Players => WarcraftPlayers.Values.ToList();

        public Config Config { get; set; } = null!;

        public WarcraftPlayer GetWcPlayer(CCSPlayerController player)
        {
            WarcraftPlayers.TryGetValue(player.Handle, out var wcPlayer);
            if (wcPlayer == null)
            {
                if (player.IsValid && !player.IsBot)
                {
                    WarcraftPlayers[player.Handle] = _database.LoadClientFromDatabase(player, XpSystem);
                }
                else
                {
                    return null;
                }
            }

            return WarcraftPlayers[player.Handle];
        }

        public void SetWcPlayer(CCSPlayerController player, WarcraftPlayer wcPlayer)
        {
            WarcraftPlayers[player.Handle] = wcPlayer;
        }

        public static void RefreshPlayerName(WarcraftPlayer wcPlayer)
        {
            if (wcPlayer == null || !wcPlayer.Player.IsValid) return;

            var playerNameClean = Regex.Replace(wcPlayer.Player.PlayerName, @"\d+ \[\w+\] ", "");
            wcPlayer.Player.PlayerName = $"{wcPlayer.currentLevel} [{wcPlayer.GetClass().DisplayName}] {playerNameClean}";
            wcPlayer.Player.Clan = "";
            Utilities.SetStateChanged(wcPlayer.Player, "CBasePlayerController", "m_iszPlayerName");
            Utilities.SetStateChanged(wcPlayer.Player, "CCSPlayerController", "m_szClan");
        }

        public override void Load(bool hotReload)
        {
            base.Load(hotReload);
            MenuAPI.Load(this, hotReload);

            _instance ??= this;

            XpSystem = new XpSystem(this);
            XpSystem.GenerateXpCurve(110, 1.07f, MaxLevel);

            _database = new Database();
            classManager = new RaceManager();
            classManager.Initialize();

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

            AddCommand("changerace", "change class", CommandChangeRace);
            AddCommand("changeclass", "change class", CommandChangeRace);
            AddCommand("race", "change class", CommandChangeRace);
            AddCommand("class", "change class", CommandChangeRace);
            AddCommand("rpg", "change class", CommandChangeRace);
            AddCommand("cr", "change class", CommandChangeRace);

            AddCommand("reset", "reset skills", CommandResetSkills);
            AddCommand("factoryreset", "reset levels", CommandFactoryReset);

            AddCommand("addxp", "addxp", CommandAddXp);

            AddCommand("skills", "skills", (client, _) => ShowSkillPointMenu2(GetWcPlayer(client)));
            AddCommand("level", "skills", (client, _) => ShowSkillPointMenu2(GetWcPlayer(client)));

            AddCommand("rpg_help", "list all commands", CommandHelp);
            AddCommand("commands", "list all commands", CommandHelp);
            AddCommand("wcs", "list all commands", CommandHelp);
            AddCommand("war3menu", "list all commands", CommandHelp);

            RegisterListener<Listeners.OnClientConnect>(OnClientPutInServerHandler);
            RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);
            RegisterListener<Listeners.OnMapEnd>(OnMapEndHandler);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnectHandler);

            RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
            {
                //Characters
                manifest.AddResource("characters/models/nozb1/skeletons_player_model/skeleton_player_model_2/skeleton_nozb2_pm.vmdl");

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

                foreach (var prop in Shapeshifter.Props)
                {
                    manifest.AddResource(prop);
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

        [RequiresPermissions("@css/addxp")]
        private void CommandAddXp(CCSPlayerController? client, CommandInfo commandinfo)
        {
            if (string.IsNullOrEmpty(commandinfo.ArgByIndex(1))) return;

            var xpToAdd = Convert.ToInt32(commandinfo.ArgByIndex(1));

            Console.WriteLine($"Adding {xpToAdd} xp to player {client.PlayerName}");
            XpSystem.AddXp(client, xpToAdd);
        }

        private void CommandHelp(CCSPlayerController? player, CommandInfo commandinfo)
        {
            player.PrintToChat($" {ChatColors.Green}Type !class to change classes, !skills to level-up");
        }

        private void CommandResetSkills(CCSPlayerController? client, CommandInfo commandinfo)
        {
            var wcPlayer = GetWcPlayer(client);

            for (int i = 0; i < 4; i++)
            {
                wcPlayer.SetAbilityLevel(i, 0);
            }

            if (XpSystem.GetFreeSkillPoints(wcPlayer) > 0)
            {
                ShowSkillPointMenu2(wcPlayer);
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
            if (!player.IsValid || player.IsBot) return;

            SetWcPlayer(player, null);
            _database.SaveClientToDatabase(player);
        }

        private void OnMapStartHandler(string mapName)
        {
            AddTimer(60f, StatusUpdate, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
            AddTimer(60.0f, _database.SaveClients, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);

            // StringTables.AddFileToDownloadsTable("sound/warcraft/ui/questcompleted.mp3");
            // StringTables.AddFileToDownloadsTable("sound/warcraft/ui/gamefound.mp3");
            //
            // Server.PrecacheSound("warcraft/ui/questcompleted.mp3");
            // Server.PrecacheSound("warcraft/ui/gamefound.mp3");
            //
            //Server.PrecacheModel("models/props_office/file_cabinet_03.vmdl");
            // Server.PrecacheSound("weapons/c4/c4_click.wav");
            // Server.PrecacheSound("weapons/hegrenade/explode3.wav");
            // Server.PrecacheSound("items/battery_pickup.wav");
            BotQuota = ConVar.Find("bot_quota").GetPrimitiveValue<int>();
            Server.PrintToConsole("Map Load Warcraft\n");
        }

        private void OnMapEndHandler()
        {
            _database.SaveClients();
        }

        private void StatusUpdate()
        {
            var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            foreach (var player in playerEntities)
            {
                if (!player.IsValid) continue;

                var wcPlayer = GetWcPlayer(player);

                if (wcPlayer == null) continue;

                RefreshPlayerName(wcPlayer);
            }
        }

        private void OnClientPutInServerHandler(int slot, string name, string ipAddress)
        {
            var player = new CCSPlayerController(NativeAPI.GetEntityFromIndex(slot + 1));
            Console.WriteLine($"Put in server {player.Handle}");
            // No bots, invalid clients or non-existent clients.
            if (!player.IsValid || player.IsBot) return;

            if (!_database.ClientExistsInDatabase(player.SteamID))
            {
                _database.AddNewClientToDatabase(player);
            }

            WarcraftPlayers[player.Handle] = _database.LoadClientFromDatabase(player, XpSystem);

            Console.WriteLine("Player just connected: " + WarcraftPlayers[player.Handle]);
            AddTimer(30, () => { WarcraftPlayers[player.Handle].GetPlayer()?.ExecuteClientCommandFromServer("rpg_help"); });
        }

        private void CommandChangeRace(CCSPlayerController? client, CommandInfo commandinfo)
        {
            var menu = new ChatMenu("Change Class");
            var races = classManager.GetAllClasses();
            foreach (var race in races.OrderBy(x => x.DisplayName))
            {
                if (Config.DeactivatedClasses.Contains(race.InternalName, StringComparer.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                menu.AddMenuOption(race.DisplayName, (player, option) =>
                {
                    _database.SaveClientToDatabase(player);

                    // Dont do anything if were already that race.
                    if (race.InternalName == player.GetWarcraftPlayer().className) return;

                    player.GetWarcraftPlayer().GetClass().PlayerChangingToAnotherRace();
                    player.GetWarcraftPlayer().className = race.InternalName;

                    _database.SaveCurrentClass(player);
                    _database.LoadClientFromDatabase(player, XpSystem);

                    if (player.PawnIsAlive)
                    {
                        player.PlayerPawn.Value.CommitSuicide(false, false);
                    }

                    RefreshPlayerName(player.GetWarcraftPlayer());
                });
            }

            CounterStrikeSharp.API.Modules.Menu.MenuManager.OpenChatMenu(client, menu);
        }

        private void UltimatePressed(CCSPlayerController? client, CommandInfo commandinfo)
        {
            var warcraftPlayer = client.GetWarcraftPlayer();
            if (warcraftPlayer.GetAbilityLevel(3) < 1)
            {
                client.PrintToCenter("No levels in ultimate");
                client.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
            }
            else if (!warcraftPlayer.GetClass().IsAbilityReady(3))
            {
                client.PrintToCenter($"Ultimate ready in {Math.Ceiling(warcraftPlayer.GetClass().AbilityCooldownRemaining(3))}s");
                client.PlayLocalSound("sounds/ui/menu_invalid.vsnd");
            }
            else
            {
                GetWcPlayer(client)?.GetClass()?.InvokeAbility(3);
            }
        }

        public override void Unload(bool hotReload)
        {
            base.Unload(hotReload);
        }

        public static void ShowSkillPointMenu2(WarcraftPlayer wcPlayer)
        {
            var warcraftClass = wcPlayer.GetClass();

            var skillsMenu = Menu.MenuManager.CreateMenu(@$"<font color='{warcraftClass.DefaultColor.Name}' class='fontSize-m'>{warcraftClass.DisplayName}</font><br>
                <font color='#90EE90' class='fontSize-s'>Level up skills ({XpSystem.GetFreeSkillPoints(wcPlayer)} available)</font>");

            for (int i = 0;i < 3; i++)
            {
                var ability = warcraftClass.GetAbility(i);
                var abilityLevel = wcPlayer.GetAbilityLevel(i);

                char color = ChatColors.Gold;

                if (wcPlayer.GetAbilityLevel(i) == WarcraftPlayer.GetMaxAbilityLevel(i) || XpSystem.GetFreeSkillPoints(wcPlayer) == 0)
                {
                    color = ChatColors.Grey;
                }

                var displayString = $"<font color='white' class='fontSize-sm'>{ability.DisplayName} [{abilityLevel}/{WarcraftPlayer.GetMaxAbilityLevel(i)}]</font>";
                var subDisplayString= $"<font color='#D3D3D3' class='fontSize-s'>{ability.GetDescription(i)}</font>";

                skillsMenu.Add(displayString, subDisplayString, (p, opt) =>
                {
                    if (XpSystem.GetFreeSkillPoints(wcPlayer) > 0)
                        wcPlayer.GrantAbilityLevel(i);

                    Menu.MenuManager.OpenMainMenu(wcPlayer.Player, skillsMenu);
                });

                Menu.MenuManager.OpenMainMenu(wcPlayer.Player, skillsMenu);
            }
        }

        public static void ShowSkillPointMenu(WarcraftPlayer wcPlayer)
        {
            wcPlayer.GetPlayer().PrintToChat(" ");
            var menu = new ChatMenu($"Level up skills ({XpSystem.GetFreeSkillPoints(wcPlayer)} available)");
            var race = wcPlayer.GetClass();

            for (int i = 0; i < 4; i++)
            {
                var ability = race.GetAbility(i);
                char color = ChatColors.Gold;
                bool isUltimate = i == 3;

                if (wcPlayer.GetAbilityLevel(i) == WarcraftPlayer.GetMaxAbilityLevel(i))
                {
                    color = ChatColors.Grey;
                }

                if (isUltimate)
                {
                    if (wcPlayer.GetAbilityLevel(i) != 1 && wcPlayer.currentLevel == MaxLevel)
                    {
                        color = ChatColors.Purple;
                    }
                    else
                    {
                        color = ChatColors.Grey;
                    }
                }

                var abilityLevel = (isUltimate && wcPlayer.currentLevel < MaxLevel) ? "lvl. 16 required" : $"{wcPlayer.GetAbilityLevel(i)}/{WarcraftPlayer.GetMaxAbilityLevel(i)}";
                var displayString = $"{color}{ability.DisplayName}{ChatColors.Default} ({abilityLevel}) {ability.GetDescription(0)}";

                bool disabled = false;
                if (isUltimate)
                {
                    if (wcPlayer.currentLevel < MaxLevel) disabled = true;
                    if (wcPlayer.GetAbilityLevel(i) >= 1) disabled = true;
                }
                else
                {
                    if (wcPlayer.GetAbilityLevel(i) >= MaxSkillLevel) disabled = true;
                }

                if (XpSystem.GetFreeSkillPoints(wcPlayer) == 0) disabled = true;

                var abilityIndex = i;
                menu.AddMenuOption(displayString, (player, option) =>
                {
                    var wcPlayer = player.GetWarcraftPlayer();

                    if (XpSystem.GetFreeSkillPoints(wcPlayer) > 0)
                        wcPlayer.GrantAbilityLevel(abilityIndex);

                    //if (XpSystem.GetFreeSkillPoints(wcPlayer) > 0)
                    //{
                    ShowSkillPointMenu(wcPlayer);
                    //}
                }, disabled);
            }

            CounterStrikeSharp.API.Modules.Menu.MenuManager.OpenChatMenu(wcPlayer.GetPlayer(), menu);
            wcPlayer.GetPlayer().PrintToChat(" ");
        }

        public void OnConfigParsed(Config config)
        {
            Config = config;
        }
    }
}