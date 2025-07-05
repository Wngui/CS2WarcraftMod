using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Core.Effects.Shared
{
    public static class StunEffectExtensions
    {
        /// <summary>
        /// Stuns the player by setting their movement speed to 0 for a duration.
        /// </summary>
        /// <param name="player">The player controller to stun.</param>
        /// <param name="duration">How long the stun should last in seconds.</param>
        public static void Stun(this CCSPlayerController player, float duration, CCSPlayerController attacker = null, string abilityName = null)
        {
            if (!player.IsAlive()) return;

            new StunEffect(player, attacker, abilityName, duration).Start();
        }
    }

    public class StunEffect : WarcraftEffect
    {
        private float _originalSpeed;
        private float _originalModifier;
        private readonly CCSPlayerController _attacker;
        private readonly string _abilityName;

        public StunEffect(CCSPlayerController owner, CCSPlayerController attacker, string abilityName, float duration)
            : base(owner, duration)
        {
            _attacker = attacker;
            _abilityName = abilityName;
        }

        public override void OnStart()
        {
            if (!Owner.IsAlive()) return;
            var pawn = Owner.PlayerPawn.Value;
            _originalSpeed = pawn.MovementServices.Maxspeed;
            _originalModifier = pawn.VelocityModifier;
            pawn.MovementServices.Maxspeed = 0;
            pawn.VelocityModifier = 0;

            if (!string.IsNullOrEmpty(_abilityName))
            {
                Owner.PrintToChat($" {ChatColors.Red}Stunned by {ChatColors.Green}{_abilityName}");
                _attacker?.PrintToChat($" {ChatColors.Green}{_abilityName}{ChatColors.Default} stunned {Owner.GetRealPlayerName()}");
            }
        }

        public override void OnTick() { }

        public override void OnFinish()
        {
            if (!Owner.IsAlive()) return;
            var pawn = Owner.PlayerPawn.Value;
            pawn.MovementServices.Maxspeed = _originalSpeed;
            pawn.VelocityModifier = _originalModifier;
        }
    }
}
