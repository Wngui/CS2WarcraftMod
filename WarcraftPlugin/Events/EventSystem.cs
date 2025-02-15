using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
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
            RegisterEventHandler<EventPlayerDisconnect>(PlayerDisconnectHandler, HookMode.Pre);

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
                            spottedPlayer.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(new EventSpottedByEnemy() { UserId = spottedByPlayer });
                            spottedByPlayer.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(new EventSpottedEnemy() { UserId = spottedPlayer });
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
            return HookResult.Continue;
        }

        private HookResult RoundStart(EventRoundStart @event, GameEventInfo info)
        {
            Utilities.GetPlayers().Where(x => !x.IsBot && !x.ControllingBot).ToList().ForEach(player =>
            {
                var warcraftPlayer = player.GetWarcraftPlayer();
                var warcraftClass = warcraftPlayer?.GetClass();

                if (warcraftClass != null)
                {
                    if (warcraftPlayer.DesiredClass != null && warcraftPlayer.DesiredClass != warcraftClass.InternalName)
                    {
                        WarcraftPlugin.Instance.EffectManager.DestroyEffects(player, EffectDestroyFlags.OnChangingRace);
                        warcraftPlayer = WarcraftPlugin.Instance.ChangeClass(player, warcraftPlayer.DesiredClass);
                        warcraftClass = warcraftPlayer.GetClass();
                    }

                    warcraftClass?.InvokeEvent(@event, HookMode.Pre);

                    if (XpSystem.GetFreeSkillPoints(warcraftPlayer) > 0)
                    {
                        SkillsMenu.Show(warcraftPlayer);
                    }
                    else
                    {
                        var message = $"{warcraftClass.DisplayName} ({warcraftPlayer.currentLevel})\n" +
                        (warcraftPlayer.IsMaxLevel ? "" : $"Experience: {warcraftPlayer.currentXp}/{warcraftPlayer.amountToLevel}\n") +
                        $"{warcraftPlayer.statusMessage}";

                        player.PrintToCenter(message);
                    }

                    Server.NextFrame(() =>
                    {
                        warcraftClass.ResetCooldowns();
                    });
                }
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
                if (attackingClass.GetLastPlayerHit()?.UserId != victim.UserId)
                {
                    attackingClass.ResetKillFeedIcon();
                    attackingClass.SetLastPlayerHit(victim);
                }

                //Prevent shotguns, etc from triggering multiple hurt other events
                if (attackingClass?.LastHurtOther != Server.CurrentTime)
                {
                    attackingClass.LastHurtOther = Server.CurrentTime;
                    attackingClass.InvokeEvent(new EventPlayerHurtOther(@event.Handle), HookMode.Pre);
                }
                //Console.WriteLine($"Killfeed currently: " + attackingClass?.GetKillFeedIcon()?.ToString());
            }

            victim?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event, HookMode.Pre);

            return HookResult.Continue;
        }

        private HookResult PlayerSpawnHandler(EventPlayerSpawn @event, GameEventInfo _)
        {
            var player = @event.Userid;

            var warcraftClass = player?.GetWarcraftPlayer()?.GetClass();

            if (warcraftClass != null)
            {
                Server.NextFrame(() =>
                {
                    warcraftClass.SetDefaultAppearance();
                    warcraftClass.InvokeEvent(@event, HookMode.Pre);
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

            if (attacker.IsValid && victim.IsValid && attacker != victim && !attacker.IsBot && attacker.PlayerPawn.IsValid && attacker.PawnIsAlive && !attacker.ControllingBot)
            {
                attacker?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(new EventPlayerKilledOther(@event.Handle), HookMode.Pre);
                var weaponName = attacker.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value.DesignerName;

                int xpToAdd = 0;
                int xpHeadshot = 0;
                int xpKnife = 0;

                xpToAdd = _plugin.XpPerKill;

                if (headshot)
                    xpHeadshot = Convert.ToInt32(_plugin.XpPerKill * _plugin.XpHeadshotModifier);

                if (weaponName == "weapon_knife")
                    xpKnife = Convert.ToInt32(_plugin.XpPerKill * _plugin.XpKnifeModifier);

                xpToAdd += xpHeadshot + xpKnife;

                _plugin.XpSystem.AddXp(attacker, xpToAdd);

                string hsBonus = "";
                if (xpHeadshot != 0)
                {
                    hsBonus = $"(+{xpHeadshot} HS bonus)";
                }

                string knifeBonus = "";
                if (xpKnife != 0)
                {
                    knifeBonus = $"(+{xpKnife} knife bonus)";
                }

                string xpString = $" {ChatColors.Gold}+{xpToAdd} XP {ChatColors.Default}for killing {ChatColors.Green}{victim.PlayerName} {ChatColors.Default}{hsBonus}{knifeBonus}";

                attacker.PrintToChat(xpString);
            }

            if (victim.IsValid && attacker.IsValid && attacker != victim)
            {
                var attackerClass = attacker.GetWarcraftPlayer()?.GetClass();
                var victimClass = victim.GetWarcraftPlayer()?.GetClass();
                WarcraftPlugin.Instance.EffectManager.DestroyEffects(victim, EffectDestroyFlags.OnDeath);
                victimClass?.InvokeEvent(@event, HookMode.Pre);
                @event.Weapon = attackerClass?.GetKillFeedIcon()?.ToString() ?? @event.Weapon;
                attackerClass?.ResetKillFeedIcon();
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
            }
            return HookResult.Continue;
        }
    }
}