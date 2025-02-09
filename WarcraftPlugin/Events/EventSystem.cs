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
            RegisterEventHandler<EventPlayerDeath>(PlayerDeathHandler);
            RegisterEventHandler<EventPlayerDisconnect>(PlayerDisconnectHandler, HookMode.Pre);
            RegisterEventHandler<EventPlayerSpawn>(PlayerSpawnHandler);
            RegisterEventHandler<EventPlayerHurt>(PlayerHurtHandler, HookMode.Pre);
            RegisterEventHandler<EventRoundStart>(RoundStart);
            //RegisterEventHandler<EventRoundEnd>(RoundEnd, HookMode.Pre); // no logic, todo remove?

            // no logic, todo remove
            //RegisterEventHandler<EventItemEquip>(PlayerItemEquip); //
            //RegisterEventHandler<EventMolotovDetonate>(PlayerMolotovDetonateHandler); //
            //RegisterEventHandler<EventWeaponFire>(PlayerShoot); //
            //RegisterEventHandler<EventPlayerJump>(PlayerJump); //
            //RegisterEventHandler<EventDecoyStarted>(DecoyStart); //
            //RegisterEventHandler<EventPlayerPing>(PlayerPing); //
            //RegisterEventHandler<EventGrenadeThrown>(GrenadeThrown); //
            //RegisterEventHandler<EventSmokegrenadeDetonate>(SmokeGrenadeDetonate); //

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
                if (!spottedPlayer.IsValid()) continue;

                var spottedByMask = spottedPlayer.PlayerPawn.Value.EntitySpottedState.SpottedByMask;

                for (int i = 0; i < spottedByMask.Length; i++)
                {
                    uint mask = spottedByMask[i];
                    int baseId = i * 32;

                    while (mask != 0)
                    {
                        int playerIndex = baseId + BitOperations.TrailingZeroCount(mask) + 1; // Offset by 1 to match the 1-based index

                        if (playerDictionary.TryGetValue((uint)playerIndex, out var spottedByPlayer) && spottedByPlayer.IsValid())
                        {
                            spottedPlayer.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(new EventSpottedByEnemy() { UserId = spottedByPlayer });
                            spottedByPlayer.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(new EventSpottedEnemy() { UserId = spottedPlayer });
                        }

                        mask &= mask - 1;
                    }
                }
            }
        }

        private HookResult SmokeGrenadeDetonate(EventSmokegrenadeDetonate @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);
            return HookResult.Continue;
        }

        private HookResult GrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);
            return HookResult.Continue;
        }

        private HookResult RoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            Utilities.GetPlayers().ForEach(p => { p.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event); });
            return HookResult.Continue;
        }

        private HookResult RoundStart(EventRoundStart @event, GameEventInfo info)
        {
            Utilities.GetPlayers().ForEach(player =>
            {
                var warcraftPlayer = player.GetWarcraftPlayer();
                var warcraftClass = warcraftPlayer?.GetClass();
                warcraftPlayer?.GetClass()?.ClearTimers();
                warcraftPlayer?.GetClass()?.InvokeEvent(@event);

                if (warcraftClass != null)
                {
                    if (warcraftPlayer.DesiredClass != null && warcraftPlayer.DesiredClass != warcraftClass.InternalName)
                    {
                        WarcraftPlugin.Instance.ChangeClass(player, warcraftPlayer.DesiredClass);
                    }

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

        private HookResult PlayerPing(EventPlayerPing @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);
            return HookResult.Continue;
        }

        private HookResult DecoyStart(EventDecoyStarted @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);
            return HookResult.Continue;
        }

        private HookResult PlayerJump(EventPlayerJump @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);
            return HookResult.Continue;
        }

        // Error handler method to ignore errors during deserialization
        static void HandleDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs errorArgs)
        {
            errorArgs.ErrorContext.Handled = true; // Ignore errors
        }

        private HookResult PlayerShoot(EventWeaponFire @event, GameEventInfo info)
        {
            var shooter = @event.Userid;
            shooter?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);

            return HookResult.Continue;
        }

        private HookResult PlayerItemEquip(EventItemEquip @event, GameEventInfo info)
        {
            var equipper = @event.Userid;
            equipper?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);

            return HookResult.Continue;
        }

        private HookResult PlayerHurtHandler(EventPlayerHurt @event, GameEventInfo _)
        {
            var victim = @event.Userid;
            var attacker = @event.Attacker;

            if (victim != null && (!victim.IsValid())) return HookResult.Continue;
            if (attacker != null && (!attacker.IsValid() || attacker.ControllingBot)) return HookResult.Continue;

            //Prevent shotguns, etc from triggering multiple hurt other events
            var attackingClass = attacker?.GetWarcraftPlayer()?.GetClass();
            if (attackingClass != null && attackingClass?.LastHurtOther != Server.CurrentTime)
            {
                attackingClass.LastHurtOther = Server.CurrentTime;
                attackingClass.InvokeEvent(new EventPlayerHurtOther(@event.Handle));
            }

            victim?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);

            return HookResult.Continue;
        }

        private HookResult PlayerSpawnHandler(EventPlayerSpawn @event, GameEventInfo _)
        {
            var player = @event.Userid;

            var warcraftPlayer = player.GetWarcraftPlayer();
            var warcraftClass = warcraftPlayer?.GetClass();

            if (warcraftClass != null)
            {
                Server.NextFrame(() =>
                {
                    warcraftClass.SetDefaultAppearance();
                    warcraftClass.ClearTimers();
                    warcraftClass.InvokeEvent(@event);
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
                attacker?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(new EventPlayerKilledOther(@event.Handle));
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
                victim?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);
            }

            victim?.GetWarcraftPlayer()?.GetClass()?.ClearTimers();

            return HookResult.Continue;
        }

        private HookResult PlayerDisconnectHandler(EventPlayerDisconnect @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player != null && player.IsValid)
            {
                var mockDeathEvent = new EventPlayerDeath(0);
                mockDeathEvent.Userid = @event.Userid;
                var warcraftPlayer = player?.GetWarcraftPlayer()?.GetClass();
                warcraftPlayer?.ClearTimers();
                warcraftPlayer?.InvokeEvent(mockDeathEvent);
            }
            return HookResult.Continue;
        }

        private HookResult PlayerMolotovDetonateHandler(EventMolotovDetonate @event, GameEventInfo _)
        {
            var attacker = @event.Userid;
            attacker?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent(@event);

            return HookResult.Continue;
        }
    }
}