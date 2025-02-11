using CounterStrikeSharp.API.Core;
using System;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Events
{
    public static class EventExtensions
    {
        /// <summary>
        /// Inflicts bonus damage to the player.
        /// </summary>
        /// <param name="damageHealth">The amount of damage to inflict.</param>
        /// <param name="damageArmor">The amount of armor damage to inflict.</param>
        /// <param name="killFeedIcon">Optional kill feed icon to display if the bonus damage results in a kill.</param>
        /// <param name="forceClientUpdate">Force client UI to update new health values.</param>
        public static void AddBonusDamage(this EventPlayerHurtOther @event, int damageHealth, int damageArmor = 0, KillFeedIcon? killFeedIcon = null)
        {
            var victim = @event.Userid;
            if (victim.IsValid())
            {
                victim.PlayerPawn.Value.Health -= damageHealth;
                victim.PlayerPawn.Value.ArmorValue -= damageArmor;
                @event.DmgHealth += damageHealth;
                @event.DmgArmor += damageArmor;

                if (killFeedIcon != null) victim.GetWarcraftPlayer(includeBot: true)?.SetKillFeedIcon(killFeedIcon);
            }
        }

        /// <summary>
        /// Ignores all incoming damage to the player. Only works in combination with Hookmode.Pre
        /// </summary>
        /// <param name="damageHealth">Optional amount of health damage to ignore.</param>
        /// <param name="damageArmor">Optional amount of armor damage to ignore.</param>
        public static void IgnoreDamage(this EventPlayerHurt @event, int? healthDamageToIgnore = null, int? armorDamageToIgnore = null)
        {
            var victim = @event.Userid;
            if (victim.IsValid())
            {
                victim.PlayerPawn.Value.Health += Math.Clamp(healthDamageToIgnore ?? @event.DmgHealth, 0, @event.DmgHealth);
                victim.PlayerPawn.Value.ArmorValue += Math.Clamp(armorDamageToIgnore ?? @event.DmgArmor, 0, @event.DmgArmor);
            }
        }
    }
}
