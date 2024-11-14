using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Net.Sockets;
using WarcraftPlugin.Helpers;
using static g3.RoundRectGenerator;
using static g3.SetGroupBehavior;

namespace WarcraftPlugin.Classes.Summons
{
    public class Zombie
    {
        private Timer _loseInterestTimer;
        private readonly double LeapCooldown = 2;
        private readonly double AttackCooldown = 2;
        private readonly int Damage = 5;

        public CChicken Entity { get; set; }
        public CCSPlayerController Owner { get; }
        public CCSPlayerController Target { get; set; }
        public double LastLeapTick { get; private set; } = 0;
        public double LastAttackTick { get; private set; } = 0;

        public Zombie(CCSPlayerController owner)
        {
            Owner = owner;
            Entity = Utilities.CreateEntityByName<CChicken>("chicken");

            //if (WarcraftPlugin.Instance.Config.NecromancerUseZombieModel)
            //{
            //    Entity.SetModel("characters/models/nozb1/skeletons_player_model/skeleton_player_model_2/skeleton_nozb2_pm.vmdl");
            //}

            Entity.Teleport(Owner.CalculatePositionInFront(new Vector(10, 10, 5)), new QAngle(), new Vector());
            Entity.DispatchSpawn();

            Utility.SpawnParticle(Entity.AbsOrigin.With().Add(z: 5), "particles/entity/env_explosion/test_particle_composite_dark_outline_smoke.vpcf");

            Entity.OwnerEntity.Raw = Owner.PlayerPawn.Raw;
            FollowLeader();
        }

        public void Update()
        {
            if (Entity == null || !Entity.IsValid) return;
            if (Owner == null || !Owner.PlayerPawn.IsValid || !Owner.PawnIsAlive) Kill();

            Vector velocity = Utility.CalculateTravelVelocity(Entity.AbsOrigin, Owner.PlayerPawn.Value.AbsOrigin, 1);

            Entity.AbsVelocity.X = Math.Clamp(velocity.X, -300, 300);
            Entity.AbsVelocity.Y = Math.Clamp(velocity.Y, -300, 300);


            if (Target != null)
            {
                if (LastLeapTick == 0 || LastLeapTick + LeapCooldown < Server.TickedTime)
                {
                    LeapTowardsTarget();
                }

                if (LastAttackTick == 0 || LastAttackTick + AttackCooldown < Server.TickedTime)
                {
                    Attack();
                }
            }
        }

        private void Attack()
        {
            var playerCollison = Target.PlayerPawn.Value.Collision.ToBox3d(Target.PlayerPawn.Value.AbsOrigin);

            //Check if zombie is inside targets collision box
            if (playerCollison.Contains(Entity.AbsOrigin.ToVector3d()))
            {
                LastAttackTick = Server.TickedTime;

                //dodamage to target
                Target.TakeDamage(5, Owner);
            }
        }

        private void LeapTowardsTarget()
        {
            LastLeapTick = Server.TickedTime;
            //Leap logic
            Vector velocity = Utility.CalculateTravelVelocity(Entity.AbsOrigin, Target.PlayerPawn.Value.AbsOrigin, 1);
            Entity.Teleport(null, null, velocity);
        }

        public void Kill()
        {
            _loseInterestTimer?.Kill();

            if (Entity == null || !Entity.IsValid) return;
            Entity.AcceptInput("Explode");
        }

        public void SetEnemy(CCSPlayerController enemy)
        {
            if (!enemy.PlayerPawn.IsValid || !enemy.PawnIsAlive) return;

            _loseInterestTimer?.Kill();
            _loseInterestTimer = WarcraftPlugin.Instance.AddTimer(5f, () =>
            {
                FollowLeader();
            });

            if (Target == enemy) { return; }

            Target = enemy;
            Entity.Leader.Raw = enemy.PlayerPawn.Raw;
        }

        private void FollowLeader()
        {
            Target = null;
            Entity.Leader.Raw = Owner.PlayerPawn.Raw;
        }
    }
}
