using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;

namespace WarcraftPlugin.Core.Effects
{
    internal class EffectManager
    {
        private readonly List<WarcraftEffect> _effects = new();
        private readonly float _tickRate = 0.25f;

        internal void Initialize()
        {
            WarcraftPlugin.Instance.AddTimer(_tickRate, EffectTick, TimerFlags.REPEAT);
        }

        internal void AddEffect(WarcraftEffect effect)
        {
            _effects.Add(effect);
            effect.OnStart();
        }

        private void EffectTick()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];

                effect.RemainingDuration -= _tickRate;
                effect.OnTick();

                if (effect.RemainingDuration <= 0)
                {
                    effect.RemainingDuration = 0;
                    effect.OnFinish();
                    _effects.RemoveAt(i);
                }
            }
        }

        internal void ClearEffects(CCSPlayerController player)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];
                if (effect.Player?.Handle == player.Handle)
                {
                    effect.OnFinish();
                    _effects.RemoveAt(i);
                }
            }
        }
    }
}