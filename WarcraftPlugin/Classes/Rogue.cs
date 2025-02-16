using System;
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using CounterStrikeSharp.API;
using WarcraftPlugin.Core.Effects;
using System.Collections.Generic;
using WarcraftPlugin.Events.ExtendedEvents;

namespace WarcraftPlugin.Classes
{
    internal class Rogue : WarcraftClass
    {
        private bool _isPlayerInvulnerable;

        public override string DisplayName => "Rogue";
        public override Color DefaultColor => Color.DarkViolet;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Stealth", "Become partially invisible for 1/2/3/4/5 seconds, when killing someone."),
            new WarcraftAbility("Sneak Attack", "When you hit an enemy in the back, you do an aditional 5/10/15/20/25 damage."),
            new WarcraftAbility("Blade Dance", "Increases movement speed and damage with knives."),
            new WarcraftCooldownAbility("Smokebomb", "When nearing death, you will automatically drop a smokebomb, letting you cheat death.", 50f)
        ];

        public override void Register()
        {
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);
            HookEvent<EventPlayerHurt>(PlayerHurt);
            HookEvent<EventPlayerKilledOther>(PlayerKilledOther);
            HookEvent<EventItemEquip>(PlayerItemEquip);
        }

        private void PlayerHurt(EventPlayerHurt @event)
        {
            if (_isPlayerInvulnerable)
            {
                Player.SetHp(1);
                return;
            }

            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            var pawn = Player.PlayerPawn.Value;
            if (pawn.Health < 0)
            {
                StartCooldown(3);
                _isPlayerInvulnerable = true;
                Player.SetHp(1);
                Player.Speed = 0;
                new InvisibleEffect(Player, 5).Start();

                //spawn smoke
                Warcraft.SpawnSmoke(Player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 5), Player.PlayerPawn.Value, Color.Black);

                //spawn molly - hack to trigger smoke faster
                var molo = Utilities.CreateEntityByName<CMolotovProjectile>("molotov_projectile");
                molo.Teleport(Player.PlayerPawn.Value.AbsOrigin, new QAngle(0, 0, 0), new Vector(0, 0, 0));
                molo.DispatchSpawn();
                molo.AcceptInput("InitializeSpawnFromWorld");

                Player.ExecuteClientCommand("slot3"); //pull out knife

                var smokeEffect = Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 90), "particles/maps/de_house/house_fireplace.vpcf");
                smokeEffect.SetParent(Player.PlayerPawn.Value);

                WarcraftPlugin.Instance.AddTimer(2f, () => _isPlayerInvulnerable = false);
            }
        }

        private void PlayerItemEquip(EventItemEquip @event)
        {
            var pawn = Player.PlayerPawn.Value;
            var activeWeaponName = pawn.WeaponServices!.ActiveWeapon.Value.DesignerName;
            if (activeWeaponName == "weapon_knife")
            {
                pawn.VelocityModifier = 1 + 0.1f * WarcraftPlayer.GetAbilityLevel(2);
            }
            else
            {
                pawn.VelocityModifier = 1;
            }
        }

        private void SetInvisible()
        {
            if (Player.PlayerPawn.Value.Render.A != 0)
            {
                new InvisibleEffect(Player, WarcraftPlayer.GetAbilityLevel(0)).Start();
            }
        }

        private void PlayerKilledOther(EventPlayerKilledOther @event)
        {
            if (WarcraftPlayer.GetAbilityLevel(0) > 0)
            {
                SetInvisible();
            }
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (!@event.Userid.IsAlive() || @event.Userid.UserId == Player.UserId) return;

            if (WarcraftPlayer.GetAbilityLevel(2) > 0) BladeDanceDamage(@event);
            if (WarcraftPlayer.GetAbilityLevel(1) > 0) Backstab(@event);
        }

        private void BladeDanceDamage(EventPlayerHurtOther @event)
        {
            if (@event.Weapon == "knife")
            {
                var damageBonus = WarcraftPlayer.GetAbilityLevel(2) * 12;
                @event.AddBonusDamage(damageBonus);
            }
        }

        private void Backstab(EventPlayerHurtOther eventPlayerHurt)
        {
            var attackerAngle = eventPlayerHurt.Attacker.PlayerPawn.Value.EyeAngles.Y;
            var victimAngle = eventPlayerHurt.Userid.PlayerPawn.Value.EyeAngles.Y;

            if (Math.Abs(attackerAngle - victimAngle) <= 50)
            {
                var damageBonus = WarcraftPlayer.GetAbilityLevel(1) * 5;
                eventPlayerHurt.AddBonusDamage(damageBonus);
                Player.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Blue}[Backstab] {damageBonus} bonus damage{ChatColors.Default}", 1);
                Warcraft.SpawnParticle(eventPlayerHurt.Userid.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 85), "particles/overhead_icon_fx/radio_voice_flash.vpcf", 1);
            }
        }

        internal class InvisibleEffect(CCSPlayerController owner, float duration) : WarcraftEffect(owner, duration)
        {
            public override void OnStart()
            {
                Owner.PrintToCenter($"[Invisible]");
                Owner.PlayerPawn.Value.SetColor(Color.FromArgb(0, 255, 255, 255));

                Owner.AdrenalineSurgeEffect(Duration);
            }
            public override void OnTick() { }
            public override void OnFinish()
            {
                Owner.GetWarcraftPlayer().GetClass().SetDefaultAppearance();
                Owner.PrintToCenter($"[Visible]");
            }
        }
    }
}