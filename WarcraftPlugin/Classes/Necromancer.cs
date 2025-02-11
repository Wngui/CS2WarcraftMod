using System;
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API;
using System.Collections.Generic;
using System.Linq;
using g3;
using WarcraftPlugin.Models;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Events;
using CounterStrikeSharp.API.Modules.Timers;
using WarcraftPlugin.Summons;

namespace WarcraftPlugin.Classes
{
    internal class Necromancer : WarcraftClass
    {
        public override string DisplayName => "Necromancer";
        public override Color DefaultColor => Color.Black;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Life Drain", "Harness dark magic to siphon health from foes and restore your own vitality."),
            new WarcraftAbility("Poison Cloud", "Infuses smoke grenades with potent toxins, damaging enemies over time."),
            new WarcraftAbility("Splintered Soul", "Chance to cheat death with a fraction of vitality."),
            new WarcraftCooldownAbility("Raise Dead", "Summon a horde of undead chicken to fight for you.", 50f)
        ];

        private readonly List<Zombie> _zombies = new();
        private const int _maxZombies = 10;
        private bool _hasCheatedDeath = true;
        private Timer _zombieUpdateTimer;

        public override void Register()
        {
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventRoundEnd>(RoundEnd);
            HookEvent<EventRoundStart>(RoundStart);
            HookEvent<EventPlayerDeath>(PlayerDeath);
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookEvent<EventGrenadeThrown>(GrenadeThrown);
            HookEvent<EventSmokegrenadeDetonate>(SmokegrenadeDetonate);
            HookEvent<EventSpottedEnemy>(SpottedPlayer);
            HookAbility(3, Ultimate);
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                var decoy = new CDecoyGrenade(Player.GiveNamedItem("weapon_smokegrenade"));
                decoy.AttributeManager.Item.CustomName = "Posion cloud";
            }
        }

        private void PlayerDeath(EventPlayerDeath death)
        {
            if (WarcraftPlayer.GetAbilityLevel(2) == 0) return;

            var pawn = Player.PlayerPawn.Value;
            if (!_hasCheatedDeath && pawn.Health <= 0)
            {
                double rolledValue = Random.Shared.NextDouble();
                float chanceToRespawn = WarcraftPlayer.GetAbilityLevel(2) / 5 * 0.80f;

                if (Warcraft.RollDice(WarcraftPlayer.GetAbilityLevel(2), 80))
                {
                    _hasCheatedDeath = true;
                    WarcraftPlugin.Instance.AddTimer(2f, () =>
                    {
                        Player.PrintToChat(" " + $"{ChatColors.DarkRed}You have cheated death, for now...{ChatColors.Default}");
                        Player.Respawn();
                        Player.SetHp(1);
                        Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_smokegrenade_init.vpcf", 2);
                        Player.PlayLocalSound("sounds/ambient/atmosphere/cs_cable_rattle02.vsnd");
                    });
                }
            }
        }

        private void SmokegrenadeDetonate(EventSmokegrenadeDetonate detonate)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                DispatchEffect(new PoisonCloud(Player, new Vector(detonate.X, detonate.Y, detonate.Z), 13));
            }
        }

        private void GrenadeThrown(EventGrenadeThrown thrown)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0 && thrown.Weapon == "smokegrenade")
            {
                var smokeGrenade = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("smokegrenade_projectile")
                    .Where(x => x.Thrower.Index == Player.PlayerPawn.Index)
                    .OrderByDescending(x => x.CreateTime).FirstOrDefault();

                if (smokeGrenade == null) return;

                var smokeColor = Color.FromArgb(100 - (int)((float)WarcraftPlayer.GetAbilityLevel(1) * (100 / 5)), 255, 0); //slight red shift 
                smokeGrenade.SmokeColor.X = smokeColor.R;
                smokeGrenade.SmokeColor.Y = smokeColor.G;
                smokeGrenade.SmokeColor.Z = smokeColor.B;
            }
        }

        private void PlayerHurtOther(EventPlayerHurt hurt)
        {
            if (!hurt.Userid.IsValid() || hurt.Userid.UserId == Player.UserId) return;

            if (Player.PlayerPawn.Value.Health < Player.PlayerPawn.Value.MaxHealth)
            {
                Warcraft.SpawnParticle(hurt.Userid.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 30), "particles/critters/chicken/chicken_impact_burst_zombie.vpcf", 10);
                var healthDrained = hurt.DmgHealth * ((float)WarcraftPlayer.GetAbilityLevel(0) / WarcraftPlugin.MaxSkillLevel * 0.3f);
                var playerCalculatedHealth = Player.PlayerPawn.Value.Health + healthDrained;
                Player.SetHp((int)Math.Min(playerCalculatedHealth, Player.PlayerPawn.Value.MaxHealth));
            }
        }

        private void RoundStart(EventRoundStart start)
        {
            _hasCheatedDeath = false;
        }

        private void RoundEnd(EventRoundEnd end)
        {
            _zombieUpdateTimer?.Kill();

            foreach (var zombie in _zombies)
            {
                zombie.Kill();
            }

            _zombies.Clear();
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            RaiseDead();
            StartCooldown(3);
        }

        private void RaiseDead()
        {
            Player.PlayLocalSound("sounds/ui/armsrace_become_leader_team.vsnd");

            for (int i = 0; i < _maxZombies; i++)
            {
                _zombies.Add(new Zombie(Player));
            }

            _zombieUpdateTimer?.Kill();

            _zombieUpdateTimer = AddTimer(0.1f, () =>
            {
                var hasValidZombies = false;
                var zombieCount = _zombies.Count;
                var zombieIndex = 0;
                foreach (var zombie in _zombies)
                {
                    if (zombie.Entity.IsValid)
                    {
                        zombieIndex++;
                        zombie.FavouritePosition = (zombieIndex * 100) / zombieCount;
                        zombie.Update();
                        hasValidZombies = true;
                    }
                }

                if (!hasValidZombies)
                {
                    _zombieUpdateTimer?.Kill();
                    _zombies.Clear();
                }
            }, TimerFlags.REPEAT);
        }

        private void SpottedPlayer(EventSpottedEnemy enemy)
        {
            foreach (var zombie in _zombies)
            {
                if (zombie.Entity.IsValid)
                    zombie.SetEnemy(enemy.UserId);
            }
        }

        internal class PoisonCloud : WarcraftEffect
        {
            private readonly Box3d _hurtBox;

            readonly int _cloudHeight = 100;
            readonly int _cloudWidth = 260;

            internal PoisonCloud(CCSPlayerController owner, Vector cloudPos, float duration)
            : base(owner, duration)
            {
                var hurtBoxPoint = cloudPos.With(z: cloudPos.Z + _cloudHeight / 2);
                _hurtBox = Warcraft.CreateBoxAroundPoint(hurtBoxPoint, _cloudWidth, _cloudWidth, _cloudHeight);
            }

            public override void OnStart()
            {
                //_hurtBox.Show(duration: Duration); //Debug
            }

            public override void OnTick()
            {
                HurtPlayersInside();
            }

            private void HurtPlayersInside()
            {
                //Find players within area
                var players = Utilities.GetPlayers();
                var playersInHurtZone = players.Where(x => x.PawnIsAlive && _hurtBox.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20)));
                //small hurt
                if (playersInHurtZone.Any())
                {
                    foreach (var player in playersInHurtZone)
                    {
                        player.TakeDamage(Player.GetWarcraftPlayer().GetAbilityLevel(1) * 2, Player, KillFeedIcon.prop_exploding_barrel);
                    }
                }
            }

            public override void OnFinish()
            {
            }
        }
    }
}