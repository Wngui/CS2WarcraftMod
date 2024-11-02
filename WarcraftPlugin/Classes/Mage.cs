using System;
using System.Drawing;
using WarcraftPlugin.Effects;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using System.Linq;
using WarcraftPlugin.Core;

namespace WarcraftPlugin.Classes
{
    public class Mage : WarcraftClass
    {
        public override string InternalName => "mage";
        public override string DisplayName => "Mage";
        public override DefaultClassModel DefaultModel => new()
        {
            //TModel = "characters/models/tm_balkan/tm_balkan_varianth.vmdl",
            //CTModel = "characters/models/ctm_swat/ctm_swat_variantg.vmdl"
        };
        public override Color DefaultColor => Color.Blue;

        private Timer _manaShieldTimer = null;

        public override void Register()
        {

            AddAbility(new WarcraftAbility("fireball", "Fireball",
                i => $"Infuses molotovs with fire magic, causing a huge explosion on impact."));

            AddAbility(new WarcraftAbility("ice_beam", "Ice beam",
                i => $"Chance to freeze enemies in place."));

            AddAbility(new WarcraftAbility("mana_shield", "Mana Shield",
                i => $"Passive magical shield, which regenerates armor over time."));

            AddAbility(new WarcraftCooldownAbility("teleport", "Teleport",
                i => $"When you press your ultimate key, you will teleport to the spot you're aiming.",
                20f));

            HookEvent<EventPlayerDeath>("player_death", PlayerDeath);
            HookEvent<EventPlayerHurt>("player_hurt_other", PlayerHurtOther);
            HookEvent<EventMolotovDetonate>("molotov_detonate", MolotovDetonate);
            HookEvent<EventPlayerSpawn>("player_spawn", PlayerSpawn);
            HookEvent<EventPlayerPing>("player_ping", PlayerPing);
            HookEvent<EventGrenadeThrown>("grenade_thrown", GrenadeThrown);

            HookAbility(3, Ultimate);
            base.Register();
        }

        private void PlayerPing(EventPlayerPing ping)
        {
            if (WarcraftPlayer.GetAbilityLevel(3) > 0 && IsAbilityReady(3))
            {
                Player.DropWeaponByDesignerName("weapon_c4");

                //To avoid getting stuck we offset towards the players original pos
                var offset = 40;
                var playerOrigin = Player.PlayerPawn.Value.AbsOrigin;
                float deltaX = playerOrigin.X - ping.X;
                float deltaY = playerOrigin.Y - ping.Y;
                float deltaZ = playerOrigin.Z - ping.Z;
                float distance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
                float newX = ping.X + deltaX / distance * offset;
                float newY = ping.Y + deltaY / distance * offset;
                float newZ = ping.Z + deltaZ / distance * offset;

                Player.PlayLocalSound("sounds/weapons/fx/nearmiss/bulletltor06.vsnd");
                Utility.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.With().Add(z: 20), "particles/ui/ui_electric_exp_glow.vpcf", 3);
                Utility.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_smokegrenade_distort.vpcf", 2);
                Player.PlayerPawn.Value.Teleport(new Vector(newX, newY, newZ), Player.PlayerPawn.Value.AbsRotation, new Vector());
                Utility.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.With().Add(z: 20), "particles/ui/ui_electric_exp_glow.vpcf", 3);
                Utility.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_smokegrenade_distort.vpcf", 2);
            }
        }

        private void PlayerSpawn(EventPlayerSpawn @event)
        {
            //Mana shield
            _manaShieldTimer?.Kill();
            if (WarcraftPlayer.GetAbilityLevel(2) > 0)
            {
                _manaShieldTimer = WarcraftPlugin.Instance.AddTimer(5 / WarcraftPlayer.GetAbilityLevel(2),
                RegenManaShield, TimerFlags.REPEAT);
            }

            //Doppelganger
            if (WarcraftPlayer.GetAbilityLevel(0) > 0)
            {
                var decoy = new CDecoyGrenade(Player.GiveNamedItem("weapon_molotov"));
                decoy.AttributeManager.Item.CustomName = "Fireball";
            }
        }

        private void RegenManaShield()
        {
            if (Player == null || !Player.PlayerPawn.IsValid || !Player.PawnIsAlive)
            {
                _manaShieldTimer?.Kill();
                return;
            }

            if (Player.PlayerPawn.Value.ArmorValue < 100)
            {
                Player.SetArmor(Player.PlayerPawn.Value.ArmorValue + 1);
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            // Hack to get players aim point in the world, see player ping event
            Player.ExecuteClientCommandFromServer("player_ping");
            StartCooldown(3);
        }

        private void PlayerHurtOther(EventPlayerHurt @event)
        {
            if (!@event.Userid.IsValid || !@event.Userid.PawnIsAlive || @event.Userid.UserId == Player.UserId) return;

            double rolledValue = Random.Shared.NextDouble();
            float chanceToStun = WarcraftPlayer.GetAbilityLevel(1) * 0.05f;

            if (rolledValue <= chanceToStun)
            {
                var victim = @event.Userid;
                DispatchEffect(new FreezeEffect(Player, victim, 1.0f));
            }
        }

        private void GrenadeThrown(EventGrenadeThrown thrown)
        {
            if (WarcraftPlayer.GetAbilityLevel(0) > 0 && thrown.Weapon == "molotov")
            {
                var molotov = Utilities.FindAllEntitiesByDesignerName<CMolotovProjectile>("molotov_projectile")
                    .Where(x => x.Thrower.Index == Player.PlayerPawn.Index)
                    .OrderByDescending(x => x.CreateTime).FirstOrDefault();

                if (molotov == null) return;

                molotov.SetModel("models/weapons/w_muzzlefireshape.vmdl");
                molotov.SetColor(Color.OrangeRed);

                var particle = Utility.SpawnParticle(molotov.AbsOrigin, "particles/inferno_fx/molotov_fire01.vpcf");
                particle.SetParent(molotov);

                Vector velocity = Player.CalculateVelocityAwayFromPlayer(1800);
                molotov.Teleport(Player.CalculatePositionInFront(new Vector(10, 10, 60)), molotov.AbsRotation, velocity);

            }
        }

        private void MolotovDetonate(EventMolotovDetonate @event)
        {
            var damage = WarcraftPlayer.GetAbilityLevel(0) * 200 * 0.2f;
            var radius = WarcraftPlayer.GetAbilityLevel(0) * 500 * 0.2f;
            Utility.SpawnExplosion(new Vector(@event.X, @event.Y, @event.Z), damage, radius, Player);
            Utility.SpawnParticle(new Vector(@event.X, @event.Y, @event.Z), "particles/survival_fx/gas_cannister_impact.vpcf");
        }

        private void PlayerDeath(EventPlayerDeath obj)
        {
            _manaShieldTimer?.Kill();
        }

        public override void PlayerChangingToAnotherRace()
        {
            _manaShieldTimer?.Kill();
        }
    }

    public class FreezeEffect : WarcraftEffect
    {
        public FreezeEffect(CCSPlayerController owner, CCSPlayerController target, float duration) : base(owner, duration, target)
        {
        }

        public override void OnStart()
        {
            Target.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Blue}[FROZEN]{ChatColors.Default}", Duration);
            var targetPlayerModel = Target.PlayerPawn.Value;

            targetPlayerModel.VelocityModifier = targetPlayerModel.VelocityModifier / 2;

            Utility.DrawLaserBetween(Owner.ToCenterOrigin(), Target.ToCenterOrigin(), Color.Cyan);
            targetPlayerModel.SetColor(Color.Cyan);
        }

        public override void OnTick()
        {
        }

        public override void OnFinish()
        {
            Target.PlayerPawn.Value.SetColor(Color.White);
            Utilities.SetStateChanged(Target.PlayerPawn.Value, "CBaseModelEntity", "m_clrRender");
        }
    }
}