using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using g3;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Drawing;
using System.Linq;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Summons
{
    internal class Boss : IDisposable
    {
        private const int _interestMax = 6;
        private int InterestScore = _interestMax;
        private readonly int _radius = 50;
        private readonly double _leapCooldown = 1;
        private readonly int _damage = 10;
        private readonly int _maxHealth = 100;

        internal int FavouritePosition { get; set; } = 1;
        internal CChicken Entity { get; set; }
        internal CCSPlayerController Owner { get; }
        internal bool IsFollowingLeader { get; private set; }
        internal CCSPlayerController Target { get; set; }
        internal double LastLeapTick { get; private set; } = 0;
        internal double LastAttackTick { get; private set; } = 0;

        //Аое урон по всем круг
        private const int _aoeDamageCircleALL = 50;
        private const int _initialRadiusCircleALL = 5;
        private const int _maxRadiusCircleALL = 1000;
        private double _lastAoETickCircleALL = 0;
        private readonly double _aoeCooldownCircleALL = 5;

        internal Boss(CCSPlayerController owner)
        {
            Owner = owner;
            Entity = Utilities.CreateEntityByName<CChicken>("chicken");
            Entity.Teleport(Owner.CalculatePositionInFront(new Vector(Random.Shared.Next(200), Random.Shared.Next(200), 5)), new QAngle(), new Vector());
            Entity.DispatchSpawn();
            Entity.SetColor(Color.GreenYellow);
            Entity.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 2.0f;
            Entity.Health = _maxHealth;
            Entity.SetModel("characters/models/ctm_heavy/ctm_heavy.vmdl");
            var _zombies = 9;
            var zombies = Utilities.GetPlayers().Where(x => x.IsBot).OrderByDescending(x => x.CreateTime).Take(_zombies).ToList();
            foreach (var zombie in zombies)
            {
                zombie.Respawn();
                zombie.PlayerPawn.Value.SetModel("characters/wcsnik/wcs/illidan/illidan.vmdl");
                zombie.PlayerPawn.Value.Teleport(Owner.CalculatePositionInFront(new Vector(10, 10, 60)), new QAngle(), new Vector());
                zombie.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 2.0f;
                zombie.RemoveWeapons();
                zombie.GiveNamedItem("weapon_knife");
                zombie.OwnerEntity.Raw = Owner.PlayerPawn.Raw;
                zombie.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = true;
                zombie.Pawn.Value.GravityScale = 0.8f;

                var zombieBot = zombie.PlayerPawn.Value.Bot;
                zombieBot.Leader.Raw = Owner.PlayerPawn.Raw;
                zombieBot.FollowTimestamp = float.MaxValue;
                zombieBot.IsFollowing = true;
                zombieBot.LookForWeaponsOnGroundTimer.Duration = float.MaxValue;

                zombie.DispatchSpawn();
            }
            Entity.SetModel("characters/models/tm_phoenix_heavy/tm_phoenix_heavy.vmdl");

            Warcraft.SpawnParticle(Entity.AbsOrigin.Clone().Add(z: 5), "particles/entity/env_explosion/test_particle_composite_dark_outline_smoke.vpcf");

            Entity.OwnerEntity.Raw = Owner.PlayerPawn.Raw;
            FollowLeader();
        }

        internal void Update()
        {
            if (Entity == null || !Entity.IsValid) return;
            if (!Owner.IsAlive()) Kill();

            if (InterestScore <= 0)
            {
                FollowLeader();
            }

            if (Target != null && Target.IsAlive())
            {
                if (LastLeapTick == 0 || LastLeapTick + _leapCooldown + Random.Shared.NextDouble() < Server.TickedTime)
                {
                    AttackLeap();
                }
            }
            else if (IsFollowingLeader)
            {
                //Ensure chicken is not stuck
                float chickenDistanceToPlayer = (Owner.PlayerPawn.Value.AbsOrigin - Entity.AbsOrigin).Length();
                Vector velocity = CircularGetVelocityToPosition(Owner.PlayerPawn.Value.AbsOrigin, Entity.AbsOrigin);

                //Give them a boost so their little chicken feet can keep up
                Entity.AbsVelocity.X = Math.Clamp(velocity.X, -300, 300);
                Entity.AbsVelocity.Y = Math.Clamp(velocity.Y, -300, 300);
                Entity.AbsVelocity.Z = 10;
            }
        }

        private Vector CircularGetVelocityToPosition(Vector circleTarget, Vector zombie, int radius = 50)
        {
            // Calculate the angle in radians (map input 1-100 to 0-2π)
            double angle = (FavouritePosition - 1) / 99.0 * 2 * Math.PI;

            // Calculate x and y offsets based on the angle and radius
            float offsetX = (float)(_radius * Math.Cos(angle));
            float offsetY = (float)(_radius * Math.Sin(angle));

            // Add these offsets to the owner's position
            Vector targetPosition = circleTarget.Clone()
                .Add(x: offsetX, y: offsetY);

            // Calculate the travel velocity
            Vector velocity = Warcraft.CalculateTravelVelocity(zombie, targetPosition, 1);
            return velocity;
        }

        private void AttackLeap()
        {
            if (Target == null) return;
            LastLeapTick = Server.TickedTime;
            Attack();

            //Leap logic
            Vector velocity = Warcraft.CalculateTravelVelocity(Entity.AbsOrigin, Target.PlayerPawn.Value.AbsOrigin, 1);

            Entity.AbsVelocity.Z = 400;
            Entity.AbsVelocity.X = Math.Clamp(velocity.X, -1000, 1000);
            Entity.AbsVelocity.Y = Math.Clamp(velocity.Y, -1000, 1000);
        }

        private void Attack()
        {
            if (Target == null) return;
            var playerCollison = Target.PlayerPawn.Value.Collision.ToBox(Target.PlayerPawn.Value.AbsOrigin.Clone().Add(z: -60));

            //Check if zombie is inside targets collision box
            new CircleAoeDamageCircle(Owner, Target);
            if (playerCollison.Contains(Entity.AbsOrigin))
            {
                //dodamage to target
                Target.TakeDamage(_damage, Owner, KillFeedIcon.fists);
                Entity.EmitSound("SprayCan.ShakeGhost", volume: 0.1f);
                InterestScore = _interestMax;
            }
            else
            {
                InterestScore--;
            }
        }

        internal void Kill()
        {
            Entity.RemoveIfValid();
        }

        internal void SetEnemy(CCSPlayerController enemy)
        {
            if (!enemy.IsAlive()) return;

            if (Target != null && Target.IsAlive())
            {
                return;
            }

            if (Target != null && Target == enemy) { return; }
            IsFollowingLeader = false;
            InterestScore = _interestMax;
            Target = enemy;
            Entity.Leader.Raw = enemy.PlayerPawn.Raw;
        }

        private void FollowLeader()
        {
            IsFollowingLeader = true;
            Target = null;
            Entity.Leader.Raw = Owner.PlayerPawn.Raw;
        }

        public void Dispose()
        {
            Kill();
        }
        private Vector AoeDamageCircle(Vector circleTarget, Vector zombie, int radius = 50)
        {
            // Calculate the angle in radians (map input 1-100 to 0-2π)
            double angle = (FavouritePosition - 1) / 99.0 * 2 * Math.PI;

            // Calculate x and y offsets based on the angle and radius
            float offsetX = (float)(_radius * Math.Cos(angle));
            float offsetY = (float)(_radius * Math.Sin(angle));

            // Add these offsets to the owner's position
            Vector targetPosition = circleTarget.Clone()
                .Add(x: offsetX, y: offsetY);

            // Calculate the travel velocity
            Vector velocity = Warcraft.CalculateTravelVelocity(zombie, targetPosition, 1);
            return velocity;
        }
        internal class CircleAoeDamageCircle(CCSPlayerController owner, CCSPlayerController victim) : WarcraftEffect(owner, onTickInterval: 2f)
        {
            private const int _maxradius = 1000;
            private const int _damagePerTick = 50;
            private const int _radiusIncrement = 100;
            private float _currentRadius = 50;
            private double _latTickTime = 0;

            public override void OnStart()
            {
                _currentRadius = _initialRadiusCircleALL;
            }
            public override void OnTick()
            {
                if (_currentRadius >= _maxradius)
                {
                    OnFinish();
                    return;
                }
                _currentRadius += _radiusIncrement;
                ApplyAoEDamage();//Нанесение урона
                //АОЕ x,y,z по кругу создавать партикли и наносить урон но нужно
                Warcraft.SpawnParticle(victim.EyePosition(-10), "particles/ui/ui_hud_kill_lvl_steamy.vpcf", 2);
                Console.WriteLine($"Current radius: {_currentRadius}");


            }
            public override void OnFinish()
            {
                Console.WriteLine("AoeDamage finished.");
            }
            private void ApplyAoEDamage()
            {//TODO ПРОВЕРКА ЕСТЬ ЛИ ИГРОК В РАДИУСЕ ЕСЛИ ДА ТО НАНОШУ УРОН...
                var enemies = Utilities.GetPlayers()
                    .Where(p => p.IsAlive() && p.TeamNum != Owner.TeamNum && (p.EyePosition() - Owner.EyePosition()).Length() <= _maxradius)
                    .ToList();
                // Получаем всех игроков в зоне действия
                foreach (var player in enemies)
                {
                    if (player.IsAlive())
                    {
                        player.TakeDamage(_damagePerTick, Owner, KillFeedIcon.disconnect);
                    }
                }
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
