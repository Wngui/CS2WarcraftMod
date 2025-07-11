using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
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

        private readonly float[] _scaleLevels = {1f, 0.85f, 0.80f, 0.75f, 0.70f, 0.65f};
        private readonly float[] _speedLevels = {1f, 1.15f, 1.20f, 1.25f, 1.30f, 1.35f};
        private readonly float[] _gravityLevels = {1f, 0.94f, 0.90f, 0.86f, 0.82f, 0.79f};
        private readonly int[] _knifeBonusMax = {0, 10, 20, 30, 40, 50};

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
                Player.PlayerPawn.Value.SetScale(_scaleLevels[shrinkLevel]);

                // apply speed and gravity
                int agilityLevel = WarcraftPlayer.GetAbilityLevel(1);
                Player.PlayerPawn.Value.VelocityModifier = _speedLevels[agilityLevel];
                Player.PlayerPawn.Value.GravityScale = _gravityLevels[agilityLevel];
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
                int bonus = Random.Shared.Next(1, _knifeBonusMax[level] + 1);
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
