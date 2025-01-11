﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Drawing;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Summons
{
    public class Zombie : IDisposable
    {
        private const int _interestMax = 6;
        private int InterestScore = _interestMax;
        private int _radius = 50;
        private readonly double LeapCooldown = 1;
        private readonly int Damage = 5;

        public int FavouritePosition { get; set; } = 1;
        public CChicken Entity { get; set; }
        public CCSPlayerController Owner { get; }
        public bool IsFollowingLeader { get; private set; }
        public CCSPlayerController Target { get; set; }
        public double LastLeapTick { get; private set; } = 0;
        public double LastAttackTick { get; private set; } = 0;

        public Zombie(CCSPlayerController owner)
        {
            Owner = owner;
            Entity = Utilities.CreateEntityByName<CChicken>("chicken");

            Entity.Teleport(Owner.CalculatePositionInFront(new Vector(Random.Shared.Next(200), Random.Shared.Next(200), 5)), new QAngle(), new Vector());
            Entity.DispatchSpawn();
            Entity.SetColor(Color.GreenYellow);
            Entity.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 2f;

            Utility.SpawnParticle(Entity.AbsOrigin.With().Add(z: 5), "particles/entity/env_explosion/test_particle_composite_dark_outline_smoke.vpcf");

            Entity.OwnerEntity.Raw = Owner.PlayerPawn.Raw;
            FollowLeader();
        }

        public void Update()
        {
            if (Entity == null || !Entity.IsValid) return;
            if (Owner == null || !Owner.IsValid || !Owner.PlayerPawn.IsValid || !Owner.PawnIsAlive) Kill();

            if(InterestScore <= 0)
            {
                FollowLeader();
            }

            if (Target != null && Target.PlayerPawn.IsValid && Target.PawnIsAlive)
            {
                if (LastLeapTick == 0 || LastLeapTick + LeapCooldown + Random.Shared.NextDouble() < Server.TickedTime)
                {
                    AttackLeap();
                }
            }
            else if(IsFollowingLeader)
            {
                //Ensure chicken is not stuck
                float chickenDistanceToPlayer = (Owner.PlayerPawn.Value.AbsOrigin - Entity.AbsOrigin).Length();
                if (chickenDistanceToPlayer > 500)
                {
                    var chickenResetPoint = Owner.CalculatePositionInFront(new Vector(Random.Shared.Next(100), Random.Shared.Next(100), 5));
                    Entity.AbsOrigin.X = chickenResetPoint.X;
                    Entity.AbsOrigin.Y = chickenResetPoint.Y;
                    Entity.AbsOrigin.Z = Owner.PlayerPawn.Value.AbsOrigin.Z+5;
                    Utility.SpawnParticle(Entity.AbsOrigin.With().Add(z: 5), "particles/entity/env_explosion/test_particle_composite_dark_outline_smoke.vpcf");
                    return;
                }
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
            Vector targetPosition = circleTarget.With()
                .Add(x: offsetX, y: offsetY);

            // Calculate the travel velocity
            Vector velocity = Utility.CalculateTravelVelocity(zombie, targetPosition, 1);
            return velocity;
        }

        private void AttackLeap()
        {
            LastLeapTick = Server.TickedTime;
            Attack();

            //Leap logic
            Vector velocity = Utility.CalculateTravelVelocity(Entity.AbsOrigin, Target.PlayerPawn.Value.AbsOrigin, 1);

            Entity.AbsVelocity.Z = 400;
            Entity.AbsVelocity.X = Math.Clamp(velocity.X, -1000, 1000);
            Entity.AbsVelocity.Y = Math.Clamp(velocity.Y, -1000, 1000);
        }

        private void Attack()
        {
            var playerCollison = Target.PlayerPawn.Value.Collision.ToBox3d(Target.PlayerPawn.Value.AbsOrigin.With().Add(z: -60));

            //Check if zombie is inside targets collision box
            if (playerCollison.Contains(Entity.AbsOrigin.ToVector3d()))
            {
                //dodamage to target
                Target.TakeDamage(Damage, Owner);
                InterestScore = _interestMax;
            }
            else
            {
                InterestScore--;
            }
        }

        public void Kill()
        {
            if (Entity == null || !Entity.IsValid) return;
            Entity.AcceptInput("Explode");
        }

        public void SetEnemy(CCSPlayerController enemy)
        {
            if (!enemy.PlayerPawn.IsValid || !enemy.PawnIsAlive) return;

            if (Target != null && Target.PlayerPawn.IsValid && Target.PawnIsAlive)
            {
                return;
            }

            if (Target == enemy) { return; }
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
    }
}
