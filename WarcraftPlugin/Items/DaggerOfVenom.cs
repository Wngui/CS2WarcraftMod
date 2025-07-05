using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Classes;
using WarcraftPlugin.Helpers;
using System.Linq;
using System.Drawing;

namespace WarcraftPlugin.Items;

internal class DaggerOfVenom : ShopItem
{
    protected override string Name => "Dagger of Venom";
    protected override string Description => "Poison enemies on hit";
    internal override int Price => 2500;
    internal override Color Color => Color.FromArgb(255, 34, 139, 34); // ForestGreen for poison/venom

    internal override void Apply(CCSPlayerController player) { }

    internal override void OnPlayerHurtOther(EventPlayerHurtOther @event)
    {
        if (@event.Attacker == null || !@event.Attacker.IsAlive()) return;
        if (@event.Userid == null || !@event.Userid.IsAlive()) return;

        var effectManager = WarcraftPlugin.Instance.EffectManager;
        var isVictimPoisoned = effectManager.GetEffectsByType<VenomStrikeEffect>()
            .Any(x => x.Victim.Handle == @event.Userid.Handle);

        if (!isVictimPoisoned)
        {
            new VenomStrikeEffect(@event.Attacker, @event.Userid, 5f, 1).Start();
        }
    }
}
