using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Core.Effects.Shared;

namespace WarcraftPlugin.Classes
{
    internal class Hammerstorm : WarcraftClass
    {
        private bool _godStrength;
        public override string DisplayName => "Hammerstorm";
        public override Color DefaultColor => Color.Gold;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Storm Bolt", "25% chance to stun enemies near your target."),
            new WarcraftAbility("Great Cleave", "25% chance to splash damage to nearby enemies."),
            new WarcraftAbility("Warcry", "Gain bonus health and movement speed."),
            new WarcraftCooldownAbility("Gods Strength", "Increase damage by 25% for 6s", 20f)
        ];

        public override void Register()
        {
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookAbility(3, Ultimate);
        }

        private void PlayerSpawn(EventPlayerSpawn @event)
        {
            Server.NextFrame(() =>
            {
                var level = WarcraftPlayer.GetAbilityLevel(2);
                var speed = 1f;
                if (level > 0)
                {
                    var healthBonus = 10 + (level - 1) * 5; //10/15/20/25/30
                    Player.SetHp(Player.PlayerPawn.Value.Health + healthBonus);
                    speed += 0.06f + (level * 0.03f);
                }
                Player.PlayerPawn.Value.VelocityModifier = speed;
            });
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (!@event.Userid.IsAlive() || @event.Userid.UserId == Player.UserId) return;

            var stormLevel = WarcraftPlayer.GetAbilityLevel(0);
            if (stormLevel > 0 && Random.Shared.Next(100) < 25)
            {
                var radius = 150 + 25 * stormLevel;
                var damage = 5 + 3 * stormLevel;
                var players = Utilities.GetPlayers().Where(x => x.PawnIsAlive && x.Team != Player.Team);
                foreach (var p in players)
                {
                    if ((p.PlayerPawn.Value.AbsOrigin - @event.Userid.PlayerPawn.Value.AbsOrigin).Length() <= radius)
                    {
                        p.Stun(0.3f, Player, GetAbility(0).DisplayName);
                        p.TakeDamage(damage, Player, KillFeedIcon.hammer);
                    }
                }
            }

            var cleaveLevel = WarcraftPlayer.GetAbilityLevel(1);
            if (cleaveLevel > 0 && Random.Shared.Next(100) < 25)
            {
                var splashPct = 0.1f + 0.05f * cleaveLevel;
                var radius = 150;
                var players = Utilities.GetPlayers().Where(x => x.PawnIsAlive && x.Team != Player.Team && x.UserId != @event.Userid.UserId);
                foreach (var p in players)
                {
                    if ((p.PlayerPawn.Value.AbsOrigin - @event.Userid.PlayerPawn.Value.AbsOrigin).Length() <= radius)
                    {
                        var bonus = @event.DmgHealth * splashPct;
                        p.TakeDamage(bonus, Player, KillFeedIcon.hammer);
                        p.PrintToChat($" {ChatColors.Red}+{(int)bonus} dmg from {ChatColors.Green}{GetAbility(1).DisplayName}");
                        Player.PrintToChat($" {ChatColors.Green}{GetAbility(1).DisplayName}{ChatColors.Default} dealt {(int)bonus} splash dmg to {p.GetRealPlayerName()}");
                    }
                }
            }

            if (_godStrength)
            {
                var extra = (int)(@event.DmgHealth * 0.25f);
                if (extra > 0)
                {
                    @event.AddBonusDamage(extra, abilityName: GetAbility(3).DisplayName);
                }
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            new GodsStrengthEffect(this, Player, 6f).Start();
            StartCooldown(3);
        }

        internal class GodsStrengthEffect(Hammerstorm cls, CCSPlayerController owner, float duration) : WarcraftEffect(owner, duration)
        {
            private readonly Hammerstorm _class = cls;

            public override void OnStart()
            {
                _class._godStrength = true;
            }

            public override void OnTick() { }

            public override void OnFinish()
            {
                _class._godStrength = false;
            }
        }
    }
}
