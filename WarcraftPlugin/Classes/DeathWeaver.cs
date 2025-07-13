using System;
using System.Drawing;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Core.Effects;
using System.Linq;

namespace WarcraftPlugin.Classes
{
    internal class DeathWeaver : WarcraftClass
    {
        public override string DisplayName => "DeathWeaver";
        public override Color DefaultColor => Color.MediumPurple;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Cripple", "25% chance to slow an enemy for 1.5/2/2.5/3/3.5 seconds."),
            new WarcraftAbility("Unholy Frenzy", "25% chance to deal 10/20/30/40/50% bonus damage."),
            new WarcraftAbility("Necromancer Master", "65/70/75/80/85% chance to spawn with an assault rifle."),
            new WarcraftCooldownAbility("Raise Skeleton", "Revive a random ally.", 30f)
        ];

        private readonly float _crippleDurationModifier = 1.5f;

        public override void Register()
        {
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookAbility(3, Ultimate);
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            var level = WarcraftPlayer.GetAbilityLevel(2);
            if (level <= 0) return;

            var chance = 60 + level * 5; //65-100%
            if (Random.Shared.Next(100) < chance)
            {
                string weapon = Player.Team == CsTeam.CounterTerrorist ? "weapon_m4a1" : "weapon_ak47";
                var gun = new CCSWeaponBaseGun(Player.GiveNamedItem(weapon));
                gun.Clip1 = gun.GetVData<CBasePlayerWeaponVData>().MaxClip1 * 2;
                Player.PrintToChat(" " + Localizer["deathweaver.spawn.rifle"]);
            }
        }

        private void PlayerHurtOther(EventPlayerHurtOther hurt)
        {
            if (!hurt.Userid.IsAlive() || hurt.Userid.UserId == Player.UserId) return;

            var crippleLevel = WarcraftPlayer.GetAbilityLevel(0);
            if (crippleLevel > 0 && Random.Shared.Next(100) < 25)
            {
                var duration = crippleLevel * _crippleDurationModifier;
                new CrippleEffect(Player, hurt.Userid, duration).Start();
            }

            var frenzyLevel = WarcraftPlayer.GetAbilityLevel(1);
            if (frenzyLevel > 0 && Random.Shared.Next(100) < 25)
            {
                var multiplier = 1.0f + 0.1f * Math.Min(frenzyLevel, 5);
                var bonusDamage = (int)(hurt.DmgHealth * (multiplier - 1.0f));
                if (bonusDamage > 0)
                {
                    hurt.AddBonusDamage(bonusDamage, abilityName: GetAbility(1).DisplayName);
                }
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            var deadTeamPlayers = Utilities.GetPlayers().Where(x => x.Team == Player.Team && !x.PawnIsAlive && x.IsValid);
            if (deadTeamPlayers.Any())
            {
                var playerToRevive = deadTeamPlayers.ElementAt(Random.Shared.Next(deadTeamPlayers.Count()));
                playerToRevive.Respawn();
                playerToRevive.PlayerPawn.Value.Teleport(Player.CalculatePositionInFront(10, 60), Player.PlayerPawn.Value.EyeAngles, new Vector());
                playerToRevive.PrintToChat($" {ChatColors.Green}Raised by {ChatColors.Default}{Player.PlayerName}!");
            }
            else
            {
                Player.PrintToChat(" " + Localizer["paladin.revive.none"]);
            }

            StartCooldown(3);
        }

        internal class CrippleEffect(CCSPlayerController owner, CCSPlayerController victim, float duration) : WarcraftEffect(owner, duration)
        {
            private readonly CCSPlayerController _victim = victim;
            private float _originalSpeed;
            private float _originalModifier;

            public override void OnStart()
            {
                if (!_victim.IsAlive()) return;
                _originalSpeed = _victim.PlayerPawn.Value.MovementServices.Maxspeed;
                _originalModifier = _victim.PlayerPawn.Value.VelocityModifier;
                _victim.PlayerPawn.Value.MovementServices.Maxspeed = _originalSpeed * 0.7f;
                _victim.PlayerPawn.Value.VelocityModifier = _originalModifier * 0.7f;
                Owner.PrintToChat($" {ChatColors.Green}{Owner.GetWarcraftPlayer().GetClass().GetAbility(0).DisplayName}{ChatColors.Default} crippled {_victim.GetRealPlayerName()}");
                _victim.PrintToChat($" {ChatColors.Red}Crippled by {ChatColors.Green}{Owner.PlayerName}");
            }

            public override void OnTick() { }

            public override void OnFinish()
            {
                if (!_victim.IsAlive()) return;
                _victim.PlayerPawn.Value.MovementServices.Maxspeed = _originalSpeed;
                _victim.PlayerPawn.Value.VelocityModifier = _originalModifier;
            }
        }
    }
}
