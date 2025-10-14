using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using WarcraftPlugin.Core;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Menu.WarcraftMenu;
using WarcraftPlugin.Models;
using static CounterStrikeSharp.API.Core.BasePlugin;

namespace WarcraftPlugin.Events
{
    internal class EventSystem
    {
        private readonly WarcraftPlugin _plugin;
        private readonly Config _config;
        private readonly List<GameAction> _gameActions = [];
        public int firstkill = 0; 

        internal EventSystem(WarcraftPlugin plugin, Config config)
        {
            _plugin = plugin;
            _config = config;
        }

        internal void Initialize()
        {
            // middleware
            RegisterEventHandler<EventPlayerSpawn>(PlayerSpawnHandler, HookMode.Pre);
            RegisterEventHandler<EventPlayerHurt>(PlayerHurtHandler, HookMode.Pre);
            RegisterEventHandler<EventPlayerDeath>(PlayerDeathHandler, HookMode.Pre);
            RegisterEventHandler<EventRoundEnd>(RoundEnd, HookMode.Pre);
            RegisterEventHandler<EventRoundStart>(RoundStart, HookMode.Pre);
            RegisterEventHandler<EventRoundStart>(RoundStartXpSupport, HookMode.Pre);
            RegisterEventHandler<EventPlayerDisconnect>(PlayerDisconnectHandler, HookMode.Pre);
            RegisterEventHandler<EventBombDefused>(BombDefused,HookMode.Pre);
            RegisterEventHandler<EventBombPlanted>(BombPlanted, HookMode.Pre);
            RegisterEventHandler<EventBombExploded>(BombExploded, HookMode.Pre);

            //Virtual functions
            VirtualFunctions.CCSPlayer_ItemServices_CanAcquireFunc.Hook(OnWeaponCanAcquire, HookMode.Pre);

            //Custom events
            _plugin.AddTimer(1, PlayerSpottedOnRadar, TimerFlags.REPEAT);

            //Register event handlers dynamically from classes
            RegisterDynamicEventHandlers();
        }

        private void RegisterEventHandler<T>(GameEventHandler<T> handler, HookMode hookMode = HookMode.Post) where T : GameEvent
        {
            if (CanAddGameAction(typeof(T), hookMode))
                _plugin.RegisterEventHandler(handler, hookMode);
        }

        private bool CanAddGameAction(Type gameEventType, HookMode hookMode)
        {
            if (typeof(ICustomGameEvent).IsAssignableFrom(gameEventType)) return false;
            if (!_gameActions.Any(x => x.EventType == gameEventType && x.HookMode == hookMode))
            {
                _gameActions.Add(new GameAction { EventType = gameEventType, HookMode = hookMode });
                return true;
            }

            //Event+Hookmode already registered
            return false;
        }

        private void RegisterDynamicEventHandlers()
        {
            foreach (var warcraftClass in _plugin.classManager.GetAllClasses())
            {
                foreach (var gameAction in warcraftClass.GetEventListeners())
                {
                    if (!CanAddGameAction(gameAction.EventType, gameAction.HookMode)) continue;

                    var handlerMethod = typeof(EventSystem).GetMethod(
                        gameAction.HookMode == HookMode.Pre ? nameof(HandleDynamicPreEvent) : nameof(HandleDynamicPostEvent),
                        BindingFlags.Static | BindingFlags.NonPublic
                    ).MakeGenericMethod(gameAction.EventType);
                    var handlerDelegate = Delegate.CreateDelegate(typeof(GameEventHandler<>).MakeGenericType(gameAction.EventType), handlerMethod);
                    var registerMethod = typeof(WarcraftPlugin).GetMethod(nameof(WarcraftPlugin.RegisterEventHandler))
                                                            .MakeGenericMethod(gameAction.EventType);
                    registerMethod.Invoke(_plugin, [handlerDelegate, HookMode.Post]);
                }
            }
        }

        private static HookResult HandleDynamicPreEvent<T>(T @event, GameEventInfo info) where T : GameEvent
        {
            return HandleDynamicEvent(@event, info, HookMode.Pre);
        }

        private static HookResult HandleDynamicPostEvent<T>(T @event, GameEventInfo info) where T : GameEvent
        {
            return HandleDynamicEvent(@event, info, HookMode.Post);
        }

        private static HookResult HandleDynamicEvent<T>(T @event, GameEventInfo info, HookMode hookMode) where T : GameEvent
        {
            var userid = @event.GetType().GetProperty("Userid")?.GetValue(@event) as CCSPlayerController;
            if (userid != null)
            {
                // Invoke player specific events directly on the affected player
                userid.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event, hookMode);
            }
            else
            {
                // Else Invoke global events on all players
                Utilities.GetPlayers().ForEach(p => { p.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event, hookMode); });
            }
            return HookResult.Continue;
        }

        private HookResult OnWeaponCanAcquire(DynamicHook hook)
        {
            CCSPlayerController client = hook.GetParam<CCSPlayer_ItemServices>(0).Pawn.Value!.Controller.Value!.As<CCSPlayerController>();

            if (client == null || !client.IsValid || !client.PawnIsAlive)
                return HookResult.Continue;

            var warcraftClass = client?.GetWarcraftPlayer()?.GetClass();
            if (warcraftClass == null)
                return HookResult.Continue;

            CCSWeaponBaseVData vdata = VirtualFunctions.GetCSWeaponDataFromKeyFunc.Invoke(-1, hook.GetParam<CEconItemView>(1).ItemDefinitionIndex.ToString());

            // Weapon is restricted
            if (warcraftClass.WeaponWhitelist.Count > 0 && !warcraftClass.WeaponWhitelist.Any(whitelistName => vdata.Name.Contains(whitelistName, StringComparison.OrdinalIgnoreCase)))
            {
                hook.SetReturn(AcquireResult.InvalidItem);
                return HookResult.Stop;
            }

            return HookResult.Continue;
        }

        private void PlayerSpottedOnRadar()
        {
            var players = Utilities.GetPlayers();
            var playerDictionary = players.ToDictionary(player => player.Index);

            foreach (var spottedPlayer in players)
            {
                if (!spottedPlayer.IsAlive()) continue;

                var spottedByMask = spottedPlayer.PlayerPawn.Value.EntitySpottedState.SpottedByMask;

                for (int i = 0; i < spottedByMask.Length; i++)
                {
                    uint mask = spottedByMask[i];
                    int baseId = i * 32;

                    while (mask != 0)
                    {
                        int playerIndex = baseId + BitOperations.TrailingZeroCount(mask) + 1; // Offset by 1 to match the 1-based index

                        if (playerDictionary.TryGetValue((uint)playerIndex, out var spottedByPlayer) && spottedByPlayer.IsAlive())
                        {
                            var spottedPlayerClass = spottedPlayer.GetWarcraftPlayer()?.GetClass();
                            var spottedByPlayerClass = spottedByPlayer.GetWarcraftPlayer()?.GetClass();

                            var eventSpottedByEnemy = new EventSpottedByEnemy() { UserId = spottedByPlayer };
                            var eventSpottedEnemy = new EventSpottedEnemy() { UserId = spottedPlayer };

                            spottedPlayerClass?.InvokeEvent(eventSpottedByEnemy, HookMode.Pre);
                            spottedByPlayerClass?.InvokeEvent(eventSpottedEnemy, HookMode.Pre);
                            spottedPlayerClass?.InvokeEvent(eventSpottedByEnemy);
                            spottedByPlayerClass?.InvokeEvent(eventSpottedEnemy);
                        }

                        mask &= mask - 1;
                    }
                }
            }
        }

        private HookResult RoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            Utilities.GetPlayers().ForEach(p =>
            {
                WarcraftPlugin.Instance.EffectManager.DestroyEffects(p, EffectDestroyFlags.OnRoundEnd);
                p.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event, HookMode.Pre);
            });

            var winnerProp = @event.GetType().GetProperty("Winner");
            if (winnerProp != null)
            {
                var value = winnerProp.GetValue(@event);
                CsTeam teamWinner;
                if (value is CsTeam enumTeam)
                    teamWinner = enumTeam;
                else
                    teamWinner = (CsTeam)Convert.ToInt32(value);

                if (teamWinner is CsTeam.Terrorist or CsTeam.CounterTerrorist)
                {
                    foreach (var player in Utilities.GetPlayers().Where(p => p.Team == teamWinner && !p.ControllingBot))
                    {
                        _plugin.XpSystem.AddXp(player, (int)_config.XpPerRoundWin);
                        var xpToAdd = _plugin.NewMethod(player, _config.XpPerRoundWin);
                        player.PrintToChat(_plugin.Localizer["xp.roundwin", xpToAdd]);
                    }
                    foreach (var player in Utilities.GetPlayers().Where(p => p.Team != teamWinner && !p.ControllingBot))
                    {
                        _plugin.XpSystem.AddXp(player, (int)_config.XpPerRoundLose);
                        var xpToAdd = _plugin.NewMethod(player, _config.XpPerRoundLose);
                        player.PrintToChat(_plugin.Localizer["xp.roundlose", xpToAdd]);
                    }
                }
            }
            return HookResult.Continue;
        }
        private HookResult BombDefused(EventBombDefused @event, GameEventInfo info)
        {
            var defuseplayer = @event.Userid;
            _plugin.XpSystem.AddXp(defuseplayer, (int)_config.XpPerDefuse);
            var xpToAdd = _plugin.NewMethod(defuseplayer, _config.XpPerDefuse);
            defuseplayer.PrintToChat(_plugin.Localizer["xp.defuse", xpToAdd]);
            return HookResult.Continue;
        }
        private HookResult BombPlanted(EventBombPlanted @event, GameEventInfo info)
        {
            var plantedplayer = @event.Userid;
            _plugin.XpSystem.AddXp(plantedplayer, (int)_config.XpPerPlanted);
            var xpToAdd = _plugin.NewMethod(plantedplayer, _config.XpPerPlanted);
            plantedplayer.PrintToChat(_plugin.Localizer["xp.plant", xpToAdd]);
            return HookResult.Continue;
        }
        private HookResult BombExploded(EventBombExploded @event, GameEventInfo info)
        {
            var explodedplayer = @event.Userid;
            _plugin.XpSystem.AddXp(explodedplayer, (int)_config.XpPerExplosion);
            var xpToAdd = _plugin.NewMethod(explodedplayer, _config.XpPerExplosion);
            explodedplayer.PrintToChat(_plugin.Localizer["xp.explode", xpToAdd]);
            return HookResult.Continue;
        }
        private HookResult RoundStartXpSupport(EventRoundStart @event, GameEventInfo info)
        {
            Utilities.GetPlayers().Where(x => !x.IsBot && !x.ControllingBot && x.GetWarcraftPlayer().GetClass().Support == true).ToList().ForEach(player =>
            {
                _plugin.XpSystem.AddXp(player, (int)_config.XpSupportClass);
                var xpToAdd = _plugin.NewMethod(player, _config.XpSupportClass);
                player.PrintToChat(_plugin.Localizer["xp.supportclass", xpToAdd]);
                player.PrintToChat("Вы саппорт + экспи");
            });
            return HookResult.Continue;
        }
        private HookResult RoundStart(EventRoundStart @event, GameEventInfo info)
        {
            Utilities.GetPlayers().Where(x => !x.IsBot && !x.ControllingBot).ToList().ForEach(player =>
            {
                var warcraftPlayer = player.GetWarcraftPlayer();
                var warcraftClass = warcraftPlayer?.GetClass();
                firstkill = 0;

                if (warcraftClass != null)
                {
                    warcraftClass?.InvokeEvent(@event, HookMode.Pre);

                    if (_plugin.fovSettings[player.Slot] == 1)
                    {
                        _plugin.AutoSpell(player);
                        SkillsMenu.Close(warcraftPlayer);
                    }
                    if (XpSystem.GetFreeSkillPoints(warcraftPlayer) > 0)
                    {
                        if (_plugin.fovSettings[player.Slot] == 0)
                        {
                            SkillsMenu.Show(warcraftPlayer);
                        }
                    }
                    else
                    {
                        var message = $"{warcraftClass.LocalizedDisplayName} ({warcraftPlayer.currentLevel})\n" +
                        (warcraftPlayer.IsMaxLevel ? "" : $"{_plugin.Localizer["xp.current"]}: {warcraftPlayer.currentXp}/{warcraftPlayer.amountToLevel}\n");

                        player.PrintToCenter(message);
                    }

                    Server.NextFrame(() =>
                    {
                        warcraftClass.ResetCooldowns();
                    });

                    warcraftPlayer.PrintItemsOwned();


                    // string xpString = $" {_plugin.Localizer["xp.roundinfo",
                    //                     warcraftPlayer?.GetClass()?.LocalizedDisplayName,
                    //                     warcraftPlayer.currentLevel,
                    //                     warcraftPlayer.currentXp,
                    //                     warcraftPlayer.amountToLevel]}";
                    // player.PrintToChat(xpString);


                    // player.PrintToChat($" {_plugin.Localizer["xp.roundinfo", 
                    //                             warcraftPlayer?.GetClass()?.LocalizedDisplayName, 
                    //                             warcraftPlayer.currentLevel,
                    //                             warcraftPlayer.currentXp, 
                    //                             warcraftPlayer.amountToLevel]}");
                    // if (!warcraftPlayer.IsMaxLevel)
                    // {
                    //     player.PrintToChat($"\n Раса: {warcraftPlayer?.GetClass()?.LocalizedDisplayName}");
                    //     player.PrintToChat($"\n Уровень: {warcraftPlayer.currentLevel}/{WarcraftPlugin.MaxLevel}");
                    //     player.PrintToChat($"\n Опыт: {warcraftPlayer.currentXp}/{warcraftPlayer.amountToLevel}");
                    // }
                    // if (!warcraftPlayer.IsMaxLevel)
                    // {
                    player.PrintToChat($"\n {ChatColors.Gold}Раса: {ChatColors.Green}{warcraftPlayer?.GetClass()?.LocalizedDisplayName}");
                    player.PrintToChat($"\n {ChatColors.Gold}Уровень: {ChatColors.Green}{warcraftPlayer.currentLevel}{ChatColors.Default}/{ChatColors.Lime}{WarcraftPlugin.MaxLevel}");
                    player.PrintToChat($"\n {ChatColors.Gold}Опыт: {ChatColors.Green}{warcraftPlayer.currentXp}{ChatColors.Default}/{ChatColors.Lime}{warcraftPlayer.amountToLevel}");
                    // }
                }
            });
            Utilities.GetPlayers().Where(x => !x.IsBot && !x.ControllingBot && x.GetWarcraftPlayer().GetClass().Support == true).ToList().ForEach(player =>
            {
                _plugin.XpSystem.AddXp(player, (int)_config.XpSupportClass);
                var xpToAdd = _plugin.NewMethod(player, _config.XpSupportClass);
                player.PrintToChat(_plugin.Localizer["xp.supportclass", xpToAdd]);
                player.PrintToChat("Вы саппорт + экспи");
            });
            return HookResult.Continue;
        }

        private HookResult PlayerHurtHandler(EventPlayerHurt @event, GameEventInfo _)
        {
            var victim = @event.Userid;
            var attacker = @event.Attacker;

            if (victim != null && (!victim.IsAlive())) return HookResult.Continue;

            var attackingClass = attacker?.GetWarcraftPlayer()?.GetClass();

            if (attackingClass != null)
            {
                if (attackingClass.GetKillFeedTick() != Server.CurrentTime)
                    attackingClass.ResetKillFeedIcon();

                //Prevent shotguns, etc from triggering multiple hurt other events
                if (attackingClass?.LastHurtOther != Server.CurrentTime)
                {
                    attackingClass.LastHurtOther = Server.CurrentTime;
                    var hurtOtherEvent = new EventPlayerHurtOther(@event.Handle);
                    attackingClass.InvokeEvent(hurtOtherEvent, HookMode.Pre);

                    ItemManager.OnPlayerHurtOther(hurtOtherEvent);
                }
            }

            victim?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event, HookMode.Pre);

            return HookResult.Continue;
        }

        private HookResult PlayerSpawnHandler(EventPlayerSpawn @event, GameEventInfo _)
        {
            var player = @event.Userid;

            var warcraftPlayer = player?.GetWarcraftPlayer();

            if (warcraftPlayer != null)
            {
                var warcraftClass = warcraftPlayer.GetClass();
                if (warcraftPlayer.DesiredClass != null && warcraftPlayer.DesiredClass != warcraftClass?.InternalName)
                {
                    WarcraftPlugin.Instance.EffectManager.DestroyEffects(player, EffectDestroyFlags.OnChangingRace);
                    warcraftPlayer = WarcraftPlugin.Instance.ChangeClass(player, warcraftPlayer.DesiredClass);
                    warcraftClass = warcraftPlayer.GetClass();
                }

                if (player.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist)
                    warcraftClass?.InvokeEvent(@event, HookMode.Pre);

                Server.NextFrame(() =>
                {
                    WarcraftPlugin.RefreshPlayerName(player);
                    warcraftClass?.SetDefaultAppearance();
                    warcraftPlayer?.ApplyItems();
                });
            }

            return HookResult.Continue;
        }

        private HookResult PlayerDeathHandler(EventPlayerDeath @event, GameEventInfo _)
        {
            var attacker = @event.Attacker;
            var victim = @event.Userid;
            var headshot = @event.Headshot;

            if (attacker == null || victim == null) return HookResult.Continue;
            

            if (attacker.IsValid && victim.IsValid && attacker != victim && attacker.PlayerPawn.IsValid && attacker.PawnIsAlive && !attacker.ControllingBot)
            {
                attacker?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(new EventPlayerKilledOther(@event.Handle), HookMode.Pre);
                var weaponName = @event.Weapon;

                if (!attacker.AllyOf(victim))
                    _plugin.XpSystem.CalculateAndAddKillXp(
                        attacker,
                        victim,
                        weaponName,
                        headshot
                    );
            }

            if (victim.IsValid && attacker.IsValid)
            {
                var attackerClass = attacker.GetWarcraftPlayer()?.GetClass();
                var victimClass = victim.GetWarcraftPlayer()?.GetClass();
                WarcraftPlugin.Instance.EffectManager.DestroyEffects(victim, EffectDestroyFlags.OnDeath);
                victimClass?.InvokeEvent(@event, HookMode.Pre);
                @event.Weapon = attackerClass?.GetKillFeedIcon()?.ToString() ?? @event.Weapon;
                victim.GetWarcraftPlayer()?.ClearItems();
            }

            return HookResult.Continue;
        }

        private HookResult PlayerDisconnectHandler(EventPlayerDisconnect @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player != null && player.IsValid)
            {
                var mockDeathEvent = new EventPlayerDeath(0) { Userid = @event.Userid };
                var warcraftPlayer = player?.GetWarcraftPlayer()?.GetClass();
                WarcraftPlugin.Instance.EffectManager.DestroyEffects(player, EffectDestroyFlags.OnDisconnect);
                warcraftPlayer?.InvokeEvent(mockDeathEvent, HookMode.Pre);

                //Автоспеел
                _plugin.fovSettings[player.Slot] = 0;
            }
            return HookResult.Continue;
        }
    }
}