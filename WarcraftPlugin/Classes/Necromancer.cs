using System;
using System.Drawing;
using WarcraftPlugin.Effects;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API;
using System.Collections.Generic;
using System.Linq;
using g3;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Races
{
    public class Necromancer : WarcraftClass
    {
        public override string InternalName => "necromancer";
        public override string DisplayName => "Necromancer";
        public override DefaultClassModel DefaultModel => new()
        {
            //TModel = "characters/models/tm_professional/tm_professional_vari.vmdl",
            //CTModel = "characters/models/ctm_sas/ctm_sas_variantg.vmdl"
        };
        public override Color DefaultColor => Color.Black;

        private readonly List<CCSPlayerController> _zombies = new();
        private const int _maxZombies = 3;
        private bool _hasCheatedDeath = true;

        public override void Register()
        {
            AddAbility(new WarcraftAbility("life_drain", "Life Drain",
                i => $"{ChatColors.BlueGrey}Harness dark magic to {ChatColors.Green}siphon health{ChatColors.BlueGrey} from foes and restore your own vitality."));

            AddAbility(new WarcraftAbility("poison_cloud", "Poison Cloud",
                i => $"{ChatColors.BlueGrey}Infuses smoke grenades with {ChatColors.Blue}potent toxins{ChatColors.BlueGrey}, damaging enemies over time."));

            AddAbility(new WarcraftAbility("splintered_soul", "Splintered Soul",
                i => $"{ChatColors.BlueGrey}Chance to {ChatColors.Yellow}cheat death{ChatColors.BlueGrey} with a fraction of vitality."));

            AddAbility(new WarcraftCooldownAbility("raise_dead", "Raise Dead",
                i => $"{ChatColors.BlueGrey}Resurrect powerful {ChatColors.Red}undead minions{ChatColors.BlueGrey} to fight alongside you.",
                50f));

            HookEvent<EventPlayerSpawn>("round_end", PlayerSpawn);
            HookEvent<EventRoundEnd>("round_end", RoundEnd);
            HookEvent<EventRoundStart>("round_start", RoundStart);
            HookEvent<EventPlayerDeath>("player_death", PlayerDeath);
            HookEvent<EventPlayerHurt>("player_hurt_other", PlayerHurtOther);
            HookEvent<EventGrenadeThrown>("grenade_thrown", GrenadeThrown);
            HookEvent<EventSmokegrenadeDetonate>("smoke_grenade_detonate", SmokegrenadeDetonate);
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
            if (!_hasCheatedDeath && pawn.Health < 0)
            {
                double rolledValue = Random.Shared.NextDouble();
                float chanceToRespawn = WarcraftPlayer.GetAbilityLevel(2) / 5 * 0.80f;

                if (rolledValue <= chanceToRespawn)
                {
                    _hasCheatedDeath = true;
                    WarcraftPlugin.Instance.AddTimer(2f, () =>
                    {
                        Player.PrintToChat(" " + $"{ChatColors.DarkRed}You have cheated death, for now...{ChatColors.Default}");
                        Player.Respawn();
                        Player.SetHp(1);
                        Utility.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_smokegrenade_init.vpcf", 2);
                        Player.PlaySound("sounds/ambient/atmosphere/cs_cable_rattle02.vsnd");
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
            if (!hurt.Userid.IsValid || !hurt.Userid.PawnIsAlive || hurt.Userid.UserId == Player.UserId) return;

            if (Player.PlayerPawn.Value.Health < Player.PlayerPawn.Value.MaxHealth)
            {
                Utility.SpawnParticle(hurt.Userid.PlayerPawn.Value.AbsOrigin.With().Add(z: 30), "particles/critters/chicken/chicken_impact_burst_zombie.vpcf", 10);
                var healthDrained = hurt.DmgHealth * ((float)WarcraftPlayer.GetAbilityLevel(0) / 5 * 0.3f);
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
            foreach (var zombie in _zombies)
            {
                Server.ExecuteCommand($"kickid {zombie.UserId}");
            }
            _zombies.Clear();
            Server.ExecuteCommand($"bot_quota {WarcraftPlugin.Instance.BotQuota}");
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            RaiseDead();
            StartCooldown(3);
        }

        private void RaiseDead()
        {
            var team = Player.Team == CsTeam.CounterTerrorist ? "ct" : "t";

            for (int i = 0; i < _maxZombies; i++)
            {
                Server.ExecuteCommand($"bot_add {team} expert");
            }

            //Delay before bot references can be grabbed
            WarcraftPlugin.Instance.AddTimer(0.1f, () =>
            {
                Utility.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.With().Add(z: 20), "particles/explosions_fx/explosion_child_water_splash03b.vpcf");
                Player.PlaySound("sounds/ui/armsrace_become_leader_team.vsnd");
                var zombies = Utilities.GetPlayers().Where(x => x.IsBot).OrderByDescending(x => x.CreateTime).Take(_maxZombies).ToList();

                foreach (var zombie in zombies)
                {
                    zombie.Respawn();
                    if (WarcraftPlugin.Instance.Config.NecromancerUseZombieModel)
                    {
                        zombie.PlayerPawn.Value.SetModel("characters/models/nozb1/skeletons_player_model/skeleton_player_model_2/skeleton_nozb2_pm.vmdl");
                    }
                    zombie.PlayerPawn.Value.Teleport(Player.CalculatePositionInFront(60, 60), new QAngle(), new Vector());
                    zombie.PlayerPawn.Value.CBodyComponent.SceneNode.GetSkeletonInstance().Scale = 0.5f;
                    zombie.RemoveWeapons();
                    zombie.GiveNamedItem("weapon_knife");
                    zombie.OwnerEntity.Raw = Player.PlayerPawn.Raw;
                    zombie.PlayerPawn.Value.WeaponServices!.PreventWeaponPickup = true;
                    zombie.Pawn.Value.GravityScale = 0.8f;

                    var zombieBot = zombie.PlayerPawn.Value.Bot;
                    zombieBot.Leader.Raw = Player.PlayerPawn.Raw;
                    zombieBot.FollowTimestamp = float.MaxValue; //Eternal servant
                    zombieBot.IsFollowing = true;
                    zombieBot.LookForWeaponsOnGroundTimer.Duration = float.MaxValue;

                    _zombies.Add(zombie);
                }
            });
        }

        public class PoisonCloud : WarcraftEffect
        {
            private readonly Box3d _hurtBox;

            readonly int _cloudHeight = 100;
            readonly int _cloudWidth = 260;

            public PoisonCloud(CCSPlayerController owner, Vector cloudPos, float duration)
            : base(owner, duration)
            {
                var hurtBoxPoint = cloudPos.With(z: cloudPos.Z + _cloudHeight / 2);
                _hurtBox = Geometry.CreateBoxAroundPoint(hurtBoxPoint, _cloudWidth, _cloudWidth, _cloudHeight);
            }

            public override void OnStart()
            {
                //Geometry.DrawVertices(_hurtBox.ComputeVertices(), duration: Duration); //debug
            }

            public override void OnTick()
            {
                HurtPlayersInside();
            }

            private void HurtPlayersInside()
            {
                //Find players within area
                var players = Utilities.GetPlayers();
                var playersInHurtZone = players.Where(x => _hurtBox.Contains(x.PlayerPawn.Value.AbsOrigin.With().Add(z: 20).ToVector3d()));
                //small hurt
                if (playersInHurtZone.Any())
                {
                    foreach (var player in playersInHurtZone)
                    {
                        player.TakeDamage(Owner.GetWarcraftPlayer().GetAbilityLevel(1) * 2, Owner);
                    }
                }
            }

            public override void OnFinish()
            {
            }
        }
    }
}