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
using CounterStrikeSharp.API.Modules.Timers;
using WarcraftPlugin.Summons;
using WarcraftPlugin.Events.ExtendedEvents;

namespace WarcraftPlugin.Classes
{
    internal class Necromancer : WarcraftClass
    {
        public override string DisplayName => "Necromancer";
        public override Color DefaultColor => Color.Black;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Life Drain", "Heal for 6/12/18/24/30% of damage dealt."),
            new WarcraftAbility("Poison Cloud", "Smoke cloud deals 2/4/6/8/10 damage per tick."),
            new WarcraftAbility("Splintered Soul", "16/32/48/64/80% chance to cheat death."),
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
            KillZombies();
            if (WarcraftPlayer.GetAbilityLevel(2) == 0) return;

            var pawn = Player.PlayerPawn.Value;
            if (!_hasCheatedDeath && pawn.Health <= 0)
            {
                if (Warcraft.RollDice(WarcraftPlayer.GetAbilityLevel(2), 80))
                {
                    _hasCheatedDeath = true;
                    WarcraftPlugin.Instance.AddTimer(2f, () =>
                    {
                        Player.PrintToChat(" " + Localizer["necromancer.cheatdeath"]);
                        Player.Respawn();
                        Player.SetHp(1);
                        Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_smokegrenade_init.vpcf", 2);
                        Player.EmitSound("Player.BecomeGhost", volume: 0.5f);
                    });
                }
            }
        }

        private void SmokegrenadeDetonate(EventSmokegrenadeDetonate detonate)
        {
            if (WarcraftPlayer.GetAbilityLevel(1) > 0)
            {
                new PoisonCloudEffect(Player, 13, new Vector(detonate.X, detonate.Y, detonate.Z)).Start();
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

        private void PlayerHurtOther(EventPlayerHurtOther hurt)
        {
            if (!hurt.Userid.IsAlive() || hurt.Userid.UserId == Player.UserId) return;

            if (Player.PlayerPawn.Value.Health < Player.PlayerPawn.Value.MaxHealth)
            {
                Warcraft.SpawnParticle(hurt.Userid.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 30), "particles/critters/chicken/chicken_impact_burst_zombie.vpcf");
                var healthDrained = hurt.DmgHealth * ((float)WarcraftPlayer.GetAbilityLevel(0) / WarcraftPlugin.MaxSkillLevel * 0.3f);
                var healAmount = Player.Heal((int)healthDrained, GetAbility(0).DisplayName);
                hurt.Userid.PrintToChat($" {Localizer["necromancer.lifedrain", Player.GetRealPlayerName(), healAmount]}");
            }
        }

        private void RoundStart(EventRoundStart start)
        {
            KillZombies();
            _hasCheatedDeath = false;
        }

        private void RoundEnd(EventRoundEnd end)
        {
            KillZombies();
        }

        private void KillZombies()
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
            Player.EmitSound("Player.BecomeGhost", volume: 0.5f);

            for (int i = 0; i < _maxZombies; i++)
            {
                _zombies.Add(new Zombie(Player));
            }

            _zombieUpdateTimer?.Kill();

            _zombieUpdateTimer = WarcraftPlugin.Instance.AddTimer(0.1f, () =>
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

        internal class PoisonCloudEffect(CCSPlayerController owner, float duration, Vector cloudPos) : WarcraftEffect(owner, duration)
        {
            readonly int _cloudHeight = 100;
            readonly int _cloudWidth = 260;
            private Box3d _hurtBox;

            public override void OnStart()
            {
                var hurtBoxPoint = cloudPos.With(z: cloudPos.Z + _cloudHeight / 2);
                _hurtBox = Warcraft.CreateBoxAroundPoint(hurtBoxPoint, _cloudWidth, _cloudWidth, _cloudHeight);
                //_hurtBox.Show(duration: Duration); //Debug
            }

            public override void OnTick()
            {
                //Find players within area
                var players = Utilities.GetPlayers();
                var playersInHurtZone = players.Where(x => x.PawnIsAlive && !x.AllyOf(Owner) && _hurtBox.Contains(x.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20)));
                //small hurt
                if (playersInHurtZone.Any())
                {
                    foreach (var player in playersInHurtZone)
                    {
                        player.TakeDamage(Owner.GetWarcraftPlayer().GetAbilityLevel(1) * 2, Owner, KillFeedIcon.prop_exploding_barrel);
                    }
                }
            }

            public override void OnFinish(){}
        }
    }
}