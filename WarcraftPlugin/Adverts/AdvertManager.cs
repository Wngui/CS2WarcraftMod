using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using System.Linq;

namespace WarcraftPlugin.Adverts
{
    internal class AdvertManager
    {
        private readonly float _interval = 180f;
        private readonly int _advertCount = WarcraftPlugin.Instance.Localizer.GetAllStrings().Count(x => x.Name.Contains("advert."));
        private int _advertIndex = 0;
        private Timer? _advertTimer;

        internal void Initialize()
        {
            _advertTimer = WarcraftPlugin.Instance.AddTimer(_interval, AdvertTick, TimerFlags.REPEAT);
        }

        internal void Cancel()
        {
            _advertTimer?.Kill();
            _advertTimer = null;
        }

        private void AdvertTick()
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (!player.IsValid || player.IsBot) continue;
                player.PrintToChat($" {WarcraftPlugin.Instance.Localizer[$"advert.{_advertIndex}"]}");
            }

            _advertIndex++;

            if(_advertIndex >= _advertCount) _advertIndex = 0; 
        }
    }
}
