using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;

namespace WarcraftPlugin.Core.Effects
{
    internal class EffectManager
    {
        private readonly List<WarcraftEffect> _effects = [];
        public static readonly float _tickRate = 0.1f; //Lowest possible interval for native timer

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

                if (effect.RemainingDuration <= 0) // Effect expired, remove it immediately
                {
                    effect.OnFinish();
                    _effects.RemoveAt(i);
                    continue;
                }

                // Run OnTick only if the specified interval has passed
                float elapsedTime = effect.Duration - effect.RemainingDuration;
                if (elapsedTime - effect.LastTick >= effect.OnTickInterval)
                {
                    effect.OnTick();
                    effect.LastTick = elapsedTime;
                }
            }
        }

        public List<WarcraftEffect> GetEffects()
        {
            return _effects;
        }

        public List<T> GetEffectsByType<T>() where T : WarcraftEffect
        {
            return GetEffects().FindAll(x => x is T).Cast<T>().ToList();
        }

        internal void DestroyEffects(CCSPlayerController player, EffectDestroyFlags flag)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];
                if (effect.Owner?.Handle == player.Handle && effect.ShouldDestroy(flag))
                {
                    if (effect.FinishOnDestroy) effect.OnFinish();
                    _effects.RemoveAt(i);
                }
            }
        }

        internal void DestroyEffect(WarcraftEffect effect)
        {
            if (_effects.Remove(effect) && effect.FinishOnDestroy)
            {
                effect.OnFinish();
            }
        }

        internal void DestroyAllEffects()
        {
            _effects.Clear();
        }
    }
}