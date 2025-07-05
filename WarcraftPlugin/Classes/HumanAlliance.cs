using System;
using System.Collections.Generic;
using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Core.Effects;

namespace WarcraftPlugin.Classes
{
    internal class HumanAlliance : WarcraftClass
    {
        public override string DisplayName => "Human Alliance";
        public override Color DefaultColor => Color.SkyBlue;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Invisibility", "Decrease visibility"),
            new WarcraftAbility("Devotion Aura", "Increase health"),
            new WarcraftAbility("Bash", "30% chance to stun on attack"),
            new WarcraftCooldownAbility("Teleport", "Push to where you aim", 10f)
        ];

        private readonly float[] _invisibilityPercent = { 0f, 0.30f, 0.36f, 0.42f, 0.50f, 0.56f };
        private readonly int[] _devotionAuraHp = { 0, 15, 25, 35, 45, 50 };
        private readonly float[] _bashDuration = { 0f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f };
        private const int BashChance = 30;
        private const float TeleportRange = 455f;

        public override void Register()
        {
            // Use Post hook so our invisibility and aura modifications apply
            // after the generic spawn handling resets player appearance.
            HookEvent<EventPlayerSpawn>(PlayerSpawn, HookMode.Post);
            HookEvent<EventPlayerHurtOther>(PlayerHurtOther);

            HookAbility(3, Ultimate);
        }

        private void PlayerSpawn(EventPlayerSpawn @event)
        {
            if (!Player.IsAlive()) return;

            var auraLevel = WarcraftPlayer.GetAbilityLevel(1);
            if (auraLevel > 0)
            {
                Server.NextFrame(() =>
                {
                    Player.SetHp(100 + _devotionAuraHp[auraLevel]);
                    Player.PlayerPawn.Value.MaxHealth = Player.PlayerPawn.Value.Health;
                });
            }
        }

        protected override void AfterSetDefaultAppearance()
        {
            var invisLevel = WarcraftPlayer.GetAbilityLevel(0);
            if (invisLevel > 0 && Player.IsValid)
            {
                var alpha = (int)(255 * (1 - _invisibilityPercent[invisLevel]));
                Player.PlayerPawn.Value.SetColor(Color.FromArgb(alpha, 255, 255, 255));
            }
        }

        private void PlayerHurtOther(EventPlayerHurtOther @event)
        {
            if (!@event.Userid.IsAlive() || @event.Userid.UserId == Player.UserId) return;

            var bashLevel = WarcraftPlayer.GetAbilityLevel(2);
            if (bashLevel > 0 && Random.Shared.Next(100) < BashChance)
            {
                new BashEffect(Player, @event.Userid, _bashDuration[bashLevel]).Start();
            }
        }

        private void Ultimate()
        {
            if (WarcraftPlayer.GetAbilityLevel(3) < 1 || !IsAbilityReady(3)) return;

            var target = Player.RayTrace();
            var origin = Player.PlayerPawn.Value.AbsOrigin;

            Vector direction;
            float distance;

            if (target == null)
            {
                // If we didn't hit anything, push in the aim direction
                direction = new Vector();
                NativeAPI.AngleVectors(Player.PlayerPawn.Value.EyeAngles.Handle, direction.Handle, nint.Zero, nint.Zero);
                distance = TeleportRange;
                direction *= TeleportRange;
            }
            else
            {
                direction = target - origin;
                distance = direction.Length();
                if (distance > TeleportRange)
                {
                    direction *= TeleportRange / distance;
                    distance = TeleportRange;
                }
            }

            Player.EmitSound("UIPanorama.equip_musicKit", volume: 0.5f);
            Warcraft.SpawnParticle(origin.Clone().Add(z: 20), "particles/ui/ui_electric_exp_glow.vpcf", 3);
            Warcraft.SpawnParticle(origin, "particles/explosions_fx/explosion_smokegrenade_distort.vpcf", 2);

            direction /= distance;
            direction *= 800f;
            if (direction.Z < 0.275f)
            {
                direction.Z = 0.275f;
            }
            Player.PlayerPawn.Value.AbsVelocity.X = direction.X;
            Player.PlayerPawn.Value.AbsVelocity.Y = direction.Y;
            Player.PlayerPawn.Value.AbsVelocity.Z = direction.Z;

            Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 20), "particles/ui/ui_electric_exp_glow.vpcf", 3);
            Warcraft.SpawnParticle(Player.PlayerPawn.Value.AbsOrigin, "particles/explosions_fx/explosion_smokegrenade_distort.vpcf", 2);
            StartCooldown(3);
        }
    }

    internal class BashEffect(CCSPlayerController owner, CCSPlayerController target, float duration) : WarcraftEffect(owner, duration)
    {
        private readonly CCSPlayerController _target = target;
        private float _originalSpeed;
        private float _originalModifier;

        public override void OnStart()
        {
            if (!_target.IsAlive()) return;
            var pawn = _target.PlayerPawn.Value;
            _originalSpeed = pawn.MovementServices.Maxspeed;
            _originalModifier = pawn.VelocityModifier;
            pawn.MovementServices.Maxspeed = 0;
            pawn.VelocityModifier = 0;
            _target.EmitSound("Default.ImpactHard", volume: 0.5f);
            Owner.PrintToChat($" {ChatColors.Green}{Owner.GetWarcraftPlayer().GetClass().GetAbility(2).DisplayName}{ChatColors.Default} stunned {_target.GetRealPlayerName()}");
            _target.PrintToChat($" {ChatColors.Red}Stunned by {ChatColors.Green}{Owner.PlayerName}");
        }
        public override void OnTick() { }
        public override void OnFinish()
        {
            if (!_target.IsAlive()) return;
            var pawn = _target.PlayerPawn.Value;
            pawn.MovementServices.Maxspeed = _originalSpeed;
            pawn.VelocityModifier = _originalModifier;
        }
    }
}
