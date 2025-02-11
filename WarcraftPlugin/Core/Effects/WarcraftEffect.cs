using CounterStrikeSharp.API.Core;

namespace WarcraftPlugin.Core.Effects
{
    public abstract class WarcraftEffect(CCSPlayerController player, float duration)
    {
        public CCSPlayerController Player { get; } = player;

        public float Duration { get; } = duration;
        public float RemainingDuration { get; set; } = duration;

        public abstract void OnStart();
        public abstract void OnTick();
        public abstract void OnFinish();
    }
}