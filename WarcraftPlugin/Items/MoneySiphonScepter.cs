using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Events.ExtendedEvents;
using System;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API;

namespace WarcraftPlugin.Items;

internal class MoneySiphonScepter : ShopItem
{
    protected override string Name => "Money Siphon Scepter";
    protected override string Description => "Steal 2% of enemy money on hit";
    internal override int Price => 3000;

    internal override void Apply(CCSPlayerController player) { }

    internal override void OnPlayerHurtOther(EventPlayerHurtOther @event)
    {
        if (@event.Attacker == null || !@event.Attacker.IsAlive()) return;
        if (@event.Userid == null || !@event.Userid.IsAlive()) return;

        try
        {
            var victimServices = @event.Userid.InGameMoneyServices;
            var attackerServices = @event.Attacker.InGameMoneyServices;
            if (victimServices == null || attackerServices == null) return;

            int victimMoney = victimServices.Account;
            int stealAmount = (int)Math.Floor(victimMoney * 0.02f);
            if (stealAmount <= 0) return;

            victimServices.Account -= stealAmount;
            attackerServices.Account += stealAmount;

            Utilities.SetStateChanged(@event.Userid, "CCSPlayerController", "m_pInGameMoneyServices");
            Utilities.SetStateChanged(@event.Attacker, "CCSPlayerController", "m_pInGameMoneyServices");
        }
        catch
        {
            // ignore if money services not available
        }
    }
}
