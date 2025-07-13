using System;
using System.Collections.Generic;
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Core.Effects;

namespace WarcraftPlugin.Classes
{
    internal class SilentAssassin : WarcraftClass
    {
        public override string DisplayName => "Silent Assassin";
        public override Color DefaultColor => Color.Gray;

        public override List<string> WeaponWhitelist => ["knife", "c4", "defuse"];

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Shrink", "Reduce model size by 15/20/25/30/35%"),
            new WarcraftAbility("Lightweight", "Increase speed and reduce gravity"),
            new WarcraftAbility("Assassin's Blade", "40% chance to add bonus knife damage"),
            // Ultimate lasts 3 seconds with a 15 second cooldown
            new WarcraftCooldownAbility("Ghost Walk", "Completely invisible for 3 seconds", 15f)
        ];

        private readonly float _scaleMultiplier = 0.5f;
        private readonly float _speedMultiplier = 0.7f;
        private readonly float _gravityMultiplier = 0.6f;
        private readonly int _knifeBonusMultiplier = 10;

        public override void Register()
        {
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookAbility(3, Ultimate);
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            Server.NextFrame(() =>
            {
                if (Player == null || !Player.IsAlive()) return;

                // apply scale
                int shrinkLevel = WarcraftPlayer.GetAbilityLevel(0);
                if (shrinkLevel > 0)
                {
                    Player.PlayerPawn.Value.SetScale(_scaleMultiplier * shrinkLevel);
                }

                // apply speed and gravity
                int lightweight = WarcraftPlayer.GetAbilityLevel(1);
                if (lightweight > 0)
                {
                    Player.PlayerPawn.Value.VelocityModifier += _speedMultiplier * lightweight;
                    Player.PlayerPawn.Value.GravityScale -= _gravityMultiplier * lightweight;
                }
            });
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (@event.Userid.AllyOf(Player)) return;
            if (@event.Weapon != "knife") return;

            int level = WarcraftPlayer.GetAbilityLevel(2);
            if (level <= 0) return;
            if (Random.Shared.NextDouble() <= 0.4)
            {
                int bonus = Random.Shared.Next(1, level * _knifeBonusMultiplier);
                @event.AddBonusDamage(bonus, abilityName: GetAbility(2).DisplayName);
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;
            new InvisibleEffect(Player, 3f).Start();
            StartCooldown(3);
        }

        private class InvisibleEffect(CCSPlayerController owner, float duration) : WarcraftEffect(owner, duration)
        {
            public override void OnStart()
            {
                if (!Owner.IsAlive()) return;
                Owner.PrintToCenter(Localizer["rogue.invsible"]);
                Owner.PlayerPawn.Value.SetColor(Color.FromArgb(0, 255, 255, 255));
            }
            public override void OnTick() { }
            public override void OnFinish()
            {
                if (!Owner.IsAlive()) return;
                Owner.GetWarcraftPlayer().GetClass().SetDefaultAppearance();
                Owner.PrintToCenter(Localizer["rogue.visible"]);
            }
        }
    }
}
