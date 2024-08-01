using System;
using System.Drawing;
using WarcraftPlugin.Effects;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using CounterStrikeSharp.API;

namespace WarcraftPlugin.Races
{
    public class Rogue : WarcraftClass
    {
        private bool _isSmokebomb;

        public override string InternalName => "rogue";
        public override string DisplayName => "Rogue";
        public override DefaultClassModel DefaultModel => new()
        {
            //TModel = "characters/models/tm_balkan/tm_balkan_variantf.vmdl",
            //CTModel = "characters/models/ctm_swat/ctm_swat_variantk.vmdl"
        };
        public override Color DefaultColor => Color.DarkViolet;

        public override void Register()
        {
            AddAbility(new WarcraftAbility("stealth", "Stealth",
                i => $"{ChatColors.BlueGrey}Become partially invisible for {ChatColors.Green}1/2/3/4/5{ChatColors.BlueGrey} seconds, when killing someone."));

            AddAbility(new WarcraftAbility("sneak_attack", "Sneak Attack",
                i => $"{ChatColors.BlueGrey}When you hit an enemy in the back, you do an aditional {ChatColors.Blue}5/10/15/20/25{ChatColors.BlueGrey} damage."));

            AddAbility(new WarcraftAbility("blade_dance", "Blade Dance",
                i => $"{ChatColors.BlueGrey}Increases movement speed and damage with {ChatColors.Yellow}knives{ChatColors.BlueGrey}."));

            AddAbility(new WarcraftCooldownAbility("smokebomb", "Smokebomb",
                i => $"{ChatColors.BlueGrey}When nearing death, you will automatically drop a {ChatColors.Red}smokebomb{ChatColors.BlueGrey}, letting you cheat death.",
                50f));

            HookEvent<EventPlayerHurt>("player_hurt_other", PlayerHurtOther);
            HookEvent<EventPlayerHurt>("player_hurt", PlayerHurt);
            HookEvent<EventPlayerDeath>("player_killed_other", PlayerKilledOther);
            HookEvent<EventItemEquip>("player_item_equip", PlayerItemEquip);
        }

        private void PlayerHurt(GameEvent @event)
        {
            if (_isSmokebomb)
            {
                Player.SetHp(1);
                return;
            }

            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            var pawn = Player.PlayerPawn.Value;
            if (pawn.Health < 0)
            {
                _isSmokebomb = true;
                Player.SetHp(1);

                //spawn smoke
                Utility.SpawnSmoke(Player.PlayerPawn.Value.AbsOrigin.With().Add(z: 5), Player.PlayerPawn.Value, Color.Black);

                //spawn molly - hack to trigger smoke faster
                var molo = Utilities.CreateEntityByName<CMolotovProjectile>("molotov_projectile");
                molo.Teleport(Player.PlayerPawn.Value.AbsOrigin, new QAngle(0, 0, 0), new Vector(0, 0, 0));
                molo.DispatchSpawn();
                molo.AcceptInput("InitializeSpawnFromWorld");

                Player.ExecuteClientCommand("slot3"); //pull out knife

                var smokeEffect = Utility.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.With().Add(z: 90), "particles/maps/de_house/house_fireplace.vpcf");
                smokeEffect.SetParent(Player.PlayerPawn.Value);

                StartCooldown(3);

                WarcraftPlugin.Instance.AddTimer(2f, ResetSmokebombCooldown);
            }
        }

        private void ResetSmokebombCooldown()
        {
            _isSmokebomb = false;
        }

        private void PlayerItemEquip(GameEvent @event)
        {
            var pawn = Player.PlayerPawn.Value;
            var activeWeaponName = pawn.WeaponServices!.ActiveWeapon.Value.DesignerName;
            if (activeWeaponName == "weapon_knife")
            {
                pawn.VelocityModifier = 1 + (0.05f * WarcraftPlayer.GetAbilityLevel(2));
            }
            else
            {
                pawn.VelocityModifier = 1;
            }
        }

        private void PlayerKilledOther(GameEvent @event)
        {
            if (WarcraftPlayer.GetAbilityLevel(0) > 0)
            {
                SetInvisible(((EventPlayerDeath)@event).Attacker);
            }
        }

        private void SetInvisible(CCSPlayerController attacker)
        {
            if (attacker.PlayerPawn.Value.Render.A != 0)
            {
                DispatchEffect(new InvisibleEffect(attacker, WarcraftPlayer.GetAbilityLevel(0)));
            }
        }

        private void PlayerHurtOther(EventPlayerHurt @event)
        {
            if (!@event.Userid.IsValid || !@event.Userid.PawnIsAlive || @event.Userid.UserId == Player.UserId) return;

            if (WarcraftPlayer.GetAbilityLevel(2) > 0) BladeDanceDamage(@event);
            if (WarcraftPlayer.GetAbilityLevel(1) > 0) Backstab(@event);
        }

        private void BladeDanceDamage(EventPlayerHurt @event)
        {
            var attackerWeapon = @event.Attacker.PlayerPawn.Value.WeaponServices!.ActiveWeapon.Value.DesignerName;

            if (attackerWeapon == "weapon_knife")
            {
                var damageBonus = WarcraftPlayer.GetAbilityLevel(2) * 12;
                @event.Userid.TakeDamage(damageBonus, Player);
            }
        }

        private void Backstab(EventPlayerHurt eventPlayerHurt)
        {
            var attackerAngle = eventPlayerHurt.Attacker.PlayerPawn.Value.EyeAngles.Y;
            var victimAngle = eventPlayerHurt.Userid.PlayerPawn.Value.EyeAngles.Y;

            if (Math.Abs(attackerAngle - victimAngle) <= 50)
            {
                var damageBonus = WarcraftPlayer.GetAbilityLevel(1) * 5;
                eventPlayerHurt.Userid.TakeDamage(damageBonus, Player);
                Player.GetWarcraftPlayer()?.SetStatusMessage($"{ChatColors.Blue}[Backstab] {damageBonus} bonus damage{ChatColors.Default}", 1);
                Utility.SpawnParticle(eventPlayerHurt.Userid.PlayerPawn.Value.AbsOrigin.With().Add(z: 85), "particles/overhead_icon_fx/radio_voice_flash.vpcf", 1);
            }
        }

        public class InvisibleEffect : WarcraftEffect
        {
            public InvisibleEffect(CCSPlayerController owner, float duration) : base(owner, duration) { }

            public override void OnStart()
            {
                Owner.PrintToCenter($"[Invisible]");
                Owner.PlayerPawn.Value.SetColor(Color.FromArgb(0, 255, 255, 255));

                Owner.PlayerPawn.Value.HealthShotBoostExpirationTime = Server.CurrentTime + Duration;
                Utilities.SetStateChanged(Owner.PlayerPawn.Value, "CCSPlayerPawn", "m_flHealthShotBoostExpirationTime");
            }

            public override void OnTick()
            {
            }

            public override void OnFinish()
            {
                Owner.PlayerPawn.Value.SetColor(Color.White);
                Owner.PrintToCenter($"[Visible]");
            }
        }
    }
}