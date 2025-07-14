using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Core.Preload;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Classes
{
    internal class DeathWeaver : WarcraftClass
    {
        public override string DisplayName => "Death Weaver";
        public override Color DefaultColor => Color.MediumPurple;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Cripple", "25% chance to slow an enemy for 1.5/2/2.5/3/3.5 seconds."),
            new WarcraftAbility("Unholy Frenzy", "25% chance to deal 10/20/30/40/50% bonus damage."),
            new WarcraftAbility("Necromancer Master", "65/70/75/80/85% chance to spawn with an assault rifle."),
            new WarcraftCooldownAbility("Raise Skeleton", "Revive a random ally.", 30f)
        ];

        private readonly float _crippleDurationModifier = 0.5f;

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
                var duration = 1 + crippleLevel * _crippleDurationModifier;
                new CrippleEffect(Player, hurt.Userid, duration).Start();
            }

            var frenzyLevel = WarcraftPlayer.GetAbilityLevel(1);
            if (frenzyLevel > 0 && Random.Shared.Next(100) < 25)
            {
                var bonusDamage = (int)(hurt.DmgHealth * (0.1f * frenzyLevel));
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

                playerToRevive.PrintToChat(" " + Localizer["paladin.revive"]);
                Utilities.GetPlayers().ForEach(x =>
                    x.PrintToChat(" " + Localizer["paladin.revive.other", playerToRevive.GetRealPlayerName(), Player.GetRealPlayerName()]));

                Server.NextFrame(() =>
                {
                    var particle = Warcraft.SpawnParticle(playerToRevive.EyePosition(-60), "particles/explosions_fx/explosion_smokegrenade_init.vpcf", 2);
                    particle.SetParent(playerToRevive.PlayerPawn.Value);
                });
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
            private CParticleSystem _particle;

            public override void OnStart()
            {
                if (!_victim.IsAlive()) return;
                _originalSpeed = _victim.PlayerPawn.Value.MovementServices.Maxspeed;
                _originalModifier = _victim.PlayerPawn.Value.VelocityModifier;
                _victim.PlayerPawn.Value.MovementServices.Maxspeed = _originalSpeed * 0.7f;
                _victim.PlayerPawn.Value.VelocityModifier = _originalModifier * 0.7f;

                Owner.PrintToChat($" {Localizer["death_weaver.cripple.other", _victim.GetRealPlayerName()]}");
                _victim.PrintToChat($" {Localizer["death_weaver.cripple", Owner.GetRealPlayerName()]}");

                _particle = Warcraft.SpawnParticle(_victim.EyePosition(-10), "particles/maps/de_dust/dust_burning_engine_fire_glow.vpcf", Duration);
                _particle.SetParent(_victim.PlayerPawn.Value);
            }

            public override void OnTick()
            {
                if (!_victim.IsAlive()) { Destroy(); return; }
            }

            public override void OnFinish()
            {
                if (_victim.IsAlive())
                {
                    _victim.PlayerPawn.Value.MovementServices.Maxspeed = _originalSpeed;
                    _victim.PlayerPawn.Value.VelocityModifier = _originalModifier;
                }

                _particle.RemoveIfValid();
            }
        }
    }
}
