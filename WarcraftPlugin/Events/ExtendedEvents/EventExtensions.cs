using CounterStrikeSharp.API.Core;
using System;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Events.ExtendedEvents
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
        public static void AddBonusDamage(this EventPlayerHurtOther @event, int damageHealth, int damageArmor = 0, KillFeedIcon? killFeedIcon = null, string abilityName = null)
        {
            var victim = @event.Userid;
            var attacker = @event.Attacker;
            if (victim.IsAlive())
            {
                victim.PlayerPawn.Value.Health -= damageHealth;
                victim.PlayerPawn.Value.ArmorValue -= damageArmor;
                @event.DmgHealth += damageHealth;
                @event.DmgArmor += damageArmor;

                var attackerClass = attacker?.GetWarcraftPlayer()?.GetClass();
                if (killFeedIcon != null) attackerClass?.SetKillFeedIcon(killFeedIcon);

                if (!string.IsNullOrEmpty(abilityName))
                {
                    if (damageHealth > 0)
                    {
                        attacker?.PrintToChat($" {WarcraftPlugin.Instance.Localizer["bonus.damage.health.attacker", damageHealth, abilityName]}");
                        victim?.PrintToChat($" {WarcraftPlugin.Instance.Localizer["bonus.damage.health.victim", damageHealth, abilityName]}");
                    }

                    if (damageArmor > 0)
                    {
                        attacker?.PrintToChat($" {WarcraftPlugin.Instance.Localizer["bonus.damage.armor.attacker", damageArmor, abilityName]}");
                        victim?.PrintToChat($" {WarcraftPlugin.Instance.Localizer["bonus.damage.armor.victim", damageArmor, abilityName]}");
                    }
                }
            }
        }

        /// <summary>
        /// Ignores all incoming damage to the player. Only works if the event is coming from Hookmode.Pre
        /// </summary>
        /// <param name="damageHealth">Optional amount of health damage to ignore.</param>
        /// <param name="damageArmor">Optional amount of armor damage to ignore.</param>
        public static void IgnoreDamage(this EventPlayerHurt @event, int? healthDamageToIgnore = null, int? armorDamageToIgnore = null)
        {
            var victim = @event.Userid;
            if (victim.IsAlive())
            {
                int ignoredHealthDamage = Math.Clamp(healthDamageToIgnore ?? @event.DmgHealth, 0, @event.DmgHealth);
                int ignoredArmorDamage = Math.Clamp(armorDamageToIgnore ?? @event.DmgArmor, 0, @event.DmgArmor);
                victim.PlayerPawn.Value.Health += ignoredHealthDamage;
                victim.PlayerPawn.Value.ArmorValue += ignoredArmorDamage;
                @event.DmgHealth -= ignoredHealthDamage;
                @event.DmgArmor -= ignoredArmorDamage;
            }
        }
    }
}
