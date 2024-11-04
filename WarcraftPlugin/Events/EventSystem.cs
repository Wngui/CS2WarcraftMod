using System;
using System.Linq;
using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Menu.WarcraftMenu;

namespace WarcraftPlugin.Events
{
    public class EventSystem
    {
        private readonly WarcraftPlugin _plugin;
        private readonly Config _config;

        public EventSystem(WarcraftPlugin plugin, Config config)
        {
            _plugin = plugin;
            _config = config;
        }

        public void Initialize()
        {
            _plugin.RegisterEventHandler<EventPlayerDeath>(PlayerDeathHandler);
            _plugin.RegisterEventHandler<EventPlayerDisconnect>(PlayerDisconnectHandler, HookMode.Pre);
            _plugin.RegisterEventHandler<EventPlayerSpawn>(PlayerSpawnHandler);
            _plugin.RegisterEventHandler<EventPlayerHurt>(PlayerHurtHandler, HookMode.Pre);
            _plugin.RegisterEventHandler<EventItemEquip>(PlayerItemEquip);
            _plugin.RegisterEventHandler<EventMolotovDetonate>(PlayerMolotovDetonateHandler);
            _plugin.RegisterEventHandler<EventWeaponFire>(PlayerShoot);
            _plugin.RegisterEventHandler<EventPlayerJump>(PlayerJump);
            _plugin.RegisterEventHandler<EventDecoyStarted>(DecoyStart);
            _plugin.RegisterEventHandler<EventPlayerPing>(PlayerPing);
            _plugin.RegisterEventHandler<EventRoundStart>(RoundStart);
            _plugin.RegisterEventHandler<EventRoundEnd>(RoundEnd, HookMode.Pre);
            _plugin.RegisterEventHandler<EventGrenadeThrown>(GrenadeThrown);
            _plugin.RegisterEventHandler<EventSmokegrenadeDetonate>(SmokeGrenadeDetonate);

            //Custom events
            _plugin.AddTimer(1, PlayerSpottedOnRadar, TimerFlags.REPEAT);
        }

        private void PlayerSpottedOnRadar()
        {
            var players = Utilities.GetPlayers();
            var playerDictionary = players.ToDictionary(player => player.Index);

            foreach (var spottedPlayer in players)
            {
                if (!spottedPlayer.IsValid || !spottedPlayer.PawnIsAlive) continue;

                var spottedByMask = spottedPlayer.PlayerPawn.Value.EntitySpottedState.SpottedByMask;

                for (int i = 0; i < spottedByMask.Length; i++)
                {
                    uint mask = spottedByMask[i];
                    int baseId = i * 32;

                    while (mask != 0)
                    {
                        int playerIndex = baseId + BitOperations.TrailingZeroCount(mask) + 1; // Offset by 1 to match the 1-based index

                        if (playerDictionary.TryGetValue((uint)playerIndex, out var spottedByPlayer) && spottedByPlayer.IsValid && spottedByPlayer.PawnIsAlive)
                        {
                            if (!spottedPlayer.IsBot)
                            {
                                spottedPlayer.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("spotted_by_player", new EventSpottedPlayer() { UserId = spottedByPlayer });
                            }

                            if (!spottedByPlayer.IsBot)
                            {
                                spottedByPlayer.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("spotted_player", new EventSpottedPlayer() { UserId = spottedPlayer });
                            }
                        }

                        mask &= mask - 1;
                    }
                }
            }
        }

        private HookResult SmokeGrenadeDetonate(EventSmokegrenadeDetonate @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("smoke_grenade_detonate", @event);
            return HookResult.Continue;
        }

        private HookResult GrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("grenade_thrown", @event);
            return HookResult.Continue;
        }

        private HookResult RoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            Utilities.GetPlayers().ForEach(p => { p.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("round_end", @event); });
            return HookResult.Continue;
        }

        private HookResult RoundStart(EventRoundStart @event, GameEventInfo info)
        {
            Utilities.GetPlayers().ForEach(player =>
            {
                var warcraftPlayer = player.GetWarcraftPlayer();
                var warcraftClass = warcraftPlayer?.GetClass();
                warcraftPlayer?.GetClass()?.InvokeEvent("round_start", @event);

                if (warcraftClass != null)
                {
                    if (_config.DeactivatedClasses.Contains(warcraftClass.InternalName, StringComparer.InvariantCultureIgnoreCase))
                    {
                        player.PrintToChat($"{ChatColors.Green}{warcraftClass.DisplayName}{ChatColors.Default} is currently {ChatColors.Red}disabled{ChatColors.Default}, please choose another class and rejoin a team.{ChatColors.Default}");
                        player.ExecuteClientCommandFromServer("class");
                        player.ChangeTeam(CsTeam.Spectator);
                        return;
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
                        WarcraftPlugin.Instance.EffectManager.ClearEffects(player);
                        warcraftClass.ResetCooldowns();
                    });
                }
            });
            return HookResult.Continue;
        }

        private HookResult PlayerPing(EventPlayerPing @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("player_ping", @event);
            return HookResult.Continue;
        }

        private HookResult DecoyStart(EventDecoyStarted @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("decoy_start", @event);
            return HookResult.Continue;
        }

        private HookResult PlayerJump(EventPlayerJump @event, GameEventInfo info)
        {
            @event.Userid?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("player_jump", @event);
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
            shooter?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("player_shoot", @event);

            return HookResult.Continue;
        }

        private HookResult PlayerItemEquip(EventItemEquip @event, GameEventInfo info)
        {
            var equipper = @event.Userid;
            equipper?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("player_item_equip", @event);

            return HookResult.Continue;
        }

        private HookResult PlayerHurtHandler(EventPlayerHurt @event, GameEventInfo _)
        {
            var victim = @event.Userid;
            var attacker = @event.Attacker;

            if (victim != null && (!victim.IsValid || !victim.PawnIsAlive)) return HookResult.Continue;
            if (attacker != null && (!attacker.IsValid || !attacker.PawnIsAlive || attacker.ControllingBot)) return HookResult.Continue;

            //Zombie attack logic
            if (attacker != null && attacker.IsBot && attacker.OwnerEntity.Value != null && (bool)attacker?.PlayerName?.Contains("zombie", StringComparison.InvariantCultureIgnoreCase))
            {
                var owner = new CCSPlayerPawn(attacker.OwnerEntity.Value.Handle);
                var controller = new CCSPlayerController(owner.Controller.Value.Handle);
                victim.SetHp(victim.PlayerPawn.Value.Health + @event.DmgHealth);
                victim.TakeDamage(@event.DmgHealth, controller, attacker);
            }

            //Prevent shotguns, etc from triggering multiple hurt other events
            var attackingClass = attacker?.GetWarcraftPlayer()?.GetClass();
            if (attackingClass != null && attackingClass?.LastHurtOther != Server.CurrentTime)
            {
                attackingClass.LastHurtOther = Server.CurrentTime;
                attackingClass.InvokeEvent("player_hurt_other", @event);
            }

            victim?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("player_hurt", @event);

            return HookResult.Continue;
        }

        private HookResult PlayerSpawnHandler(EventPlayerSpawn @event, GameEventInfo _)
        {
            var player = @event.Userid;

            var warcraftPlayer = player.GetWarcraftPlayer();
            var warcraftClass = warcraftPlayer?.GetClass();

            if (warcraftClass != null)
            {
                var name = @event.EventName;
                Server.NextFrame(() =>
                {
                    WarcraftPlugin.Instance.EffectManager.ClearEffects(player);
                    warcraftClass.SetDefaultAppearance();
                    warcraftClass.InvokeEvent(name, @event);
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
                attacker?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("player_killed_other", @event);
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
                victim?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("player_death", @event);
            }

            WarcraftPlugin.Instance.EffectManager.ClearEffects(victim);

            return HookResult.Continue;
        }

        private HookResult PlayerDisconnectHandler(EventPlayerDisconnect @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player != null && player.IsValid)
            {
                var mockDeathEvent = new EventPlayerDeath(0);
                mockDeathEvent.Userid = @event.Userid;
                player?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("player_death", mockDeathEvent);
            }
            return HookResult.Continue;
        }

        private HookResult PlayerMolotovDetonateHandler(EventMolotovDetonate @event, GameEventInfo _)
        {
            var attacker = @event.Userid;
            attacker?.GetWarcraftPlayer()?.GetClass()?.InvokeEvent("molotov_detonate", @event);

            return HookResult.Continue;
        }
    }
}