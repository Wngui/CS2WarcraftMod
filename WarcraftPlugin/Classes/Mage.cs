using System;
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using CounterStrikeSharp.API;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using System.Linq;
using WarcraftPlugin.Core.Effects;
using System.Collections.Generic;
using WarcraftPlugin.Events.ExtendedEvents;

namespace WarcraftPlugin.Classes
{
    internal class Mage : WarcraftClass
    {
        private int _tick;

        public override string DisplayName => "Mage";
        public override Color DefaultColor => Color.Blue;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Fireball", "Infuses molotovs with fire magic, causing a huge explosion on impact."),
            new WarcraftAbility("Ice Beam", "Chance to freeze enemies in place."),
            new WarcraftAbility("Mana Shield", "Passive magical shield, which regenerates armor over time."),
            new WarcraftCooldownAbility("Teleport", "When you press your ultimate key, you will teleport to the spot you're aiming.", 20f)
        ];

        public override void Register()
        {
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookEvent<EventMolotovDetonate>(MolotovDetonate);
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventPlayerPing>(PlayerPing);
            HookEvent<EventGrenadeThrown>(GrenadeThrown);

            HookAbility(3, Ultimate);
        }

        private void PlayerPing(EventPlayerPing ping)
        {
            //Teleport ultimate
            if (WarcraftPlayer.GetAbilityLevel(3) > 0 && IsAbilityReady(3))
            {
                StartCooldown(3);
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
                Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20), "particles/ui/ui_electric_exp_glow.vpcf", 3);
                Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_smokegrenade_distort.vpcf", 2);
                Player.PlayerPawn.Value.Teleport(new Vector(newX, newY, newZ), Player.PlayerPawn.Value.AbsRotation, new Vector());
                Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20), "particles/ui/ui_electric_exp_glow.vpcf", 3);
                Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_smokegrenade_distort.vpcf", 2);
            }
        }

        private void PlayerSpawn(EventPlayerSpawn @event)
        {
            //Mana shield
            var regenArmorRate = 5 / WarcraftPlayer.GetAbilityLevel(2);
            new ManaShieldEffect(Player, regenArmorRate).Start();

            //Fireball
            if (WarcraftPlayer.GetAbilityLevel(0) > 0)
            {
                var decoy = new CDecoyGrenade(Player.GiveNamedItem("weapon_molotov"));
                decoy.AttributeManager.Item.CustomName = "Fireball";
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            // Hack to get players aim point in the world, see player ping event
            // TODO replace with ray-tracing
            Player.ExecuteClientCommandFromServer("player_ping");
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (!@event.Userid.IsAlive() || @event.Userid.UserId == Player.UserId) return;

            if (Warcraft.RollDice(WarcraftPlayer.GetAbilityLevel(1), 25))
            {
                var victim = @event.Userid;
                new FreezeEffect(Player, 1.0f, victim).Start();
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

                var particle = Warcraft.SpawnParticle(molotov.AbsOrigin, "particles/inferno_fx/molotov_fire01.vpcf");
                particle.SetParent(molotov);

                Vector velocity = Player.CalculateVelocityAwayFromPlayer(1800);
                molotov.Teleport(Player.CalculatePositionInFront(new Vector(10, 10, 60)), molotov.AbsRotation, velocity);

            }
        }

        private void MolotovDetonate(EventMolotovDetonate @event)
        {
            var damage = WarcraftPlayer.GetAbilityLevel(0) * 40f;
            var radius = WarcraftPlayer.GetAbilityLevel(0) * 100f;
            Warcraft.SpawnExplosion(new Vector(@event.X, @event.Y, @event.Z), damage, radius, Player);
            Warcraft.SpawnParticle(new Vector(@event.X, @event.Y, @event.Z), "particles/survival_fx/gas_cannister_impact.vpcf");
        }
    }

    internal class ManaShieldEffect(CCSPlayerController owner, float onTickInterval) : WarcraftEffect(owner, onTickInterval: onTickInterval, duration: 10)
    {
        public override void OnStart()
        {
            if (Owner.PlayerPawn.Value.ArmorValue == 0)
            {
                Owner.GiveNamedItem("item_assaultsuit");
                Owner.SetArmor(1);
            }
        }
        public override void OnTick()
        {
            if (Owner.PlayerPawn.Value.ArmorValue < 100)
            {
                Owner.SetArmor(Owner.PlayerPawn.Value.ArmorValue + 1);
            }
        }
        public override void OnFinish(){}
    }

    internal class FreezeEffect(CCSPlayerController owner, float duration, CCSPlayerController target) : WarcraftEffect(owner, duration)
    {
        public override void OnStart()
        {
            target.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Blue}[FROZEN]{ChatColors.Default}", Duration);
            var targetPlayerModel = target.PlayerPawn.Value;

            targetPlayerModel.VelocityModifier = targetPlayerModel.VelocityModifier / 2;

            Warcraft.DrawLaserBetween(Owner.ToCenterOrigin(), target.ToCenterOrigin(), Color.Cyan);
            targetPlayerModel.SetColor(Color.Cyan);
        }
        public override void OnTick() { }
        public override void OnFinish()
        {
            target.PlayerPawn.Value.SetColor(Color.White);
        }
    }
}