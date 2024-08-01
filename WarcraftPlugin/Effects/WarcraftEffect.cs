using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Effects
{
    public abstract class WarcraftEffect
    {
        protected WarcraftEffect(CCSPlayerController owner, float duration, CCSPlayerController target = null)
        {
            Owner = owner;
            Target = target;
            Duration = duration;
            RemainingDuration = duration;
        }

        public CCSPlayerController Owner { get; }
        public CCSPlayerController Target { get; }

        public float Duration { get; }
        public float RemainingDuration { get; set; }

        public abstract void OnStart();
        public abstract void OnTick();
        public abstract void OnFinish();
    }
}