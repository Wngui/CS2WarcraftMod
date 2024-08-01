using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using WarcraftPlugin.Effects;
using CounterStrikeSharp.API.Modules.Events;
using System.Drawing;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using CounterStrikeSharp.API.Modules.Utils;

namespace WarcraftPlugin
{
    public interface IWarcraftAbility
    {
        public string InternalName { get; }
        public string DisplayName { get; }

        public string GetDescription(int abilityLevel);
    }

    public class WarcraftAbility : IWarcraftAbility
    {
        private readonly Func<int, string> _descriptionGetter;

        public WarcraftAbility(string internalName, string displayName, Func<int, string> descriptionGetter)
        {
            InternalName = internalName;
            DisplayName = displayName;
            _descriptionGetter = descriptionGetter;
        }

        public string InternalName { get; }
        public string DisplayName { get; }

        public string GetDescription(int abilityLevel)
        {
            return _descriptionGetter.Invoke(abilityLevel);
        }
    }

    public class WarcraftCooldownAbility : WarcraftAbility
    {
        public float Cooldown { get; set; } = 0f;

        public WarcraftCooldownAbility(string internalName, string displayName, Func<int, string> descriptionGetter,
            float cooldown) : base(internalName, displayName, descriptionGetter)
        {
            Cooldown = cooldown;
        }
    }

    public abstract class WarcraftClass
    {
        public abstract string InternalName { get; }
        public abstract string DisplayName { get; }
        public abstract DefaultClassModel DefaultModel { get; }
        public abstract Color DefaultColor { get; }
        public WarcraftPlayer WarcraftPlayer { get; set; }
        public CCSPlayerController Player { get; set; }

        private readonly List<IWarcraftAbility> _abilities = [];
        private readonly Dictionary<string, Action<GameEvent>> _eventHandlers = [];
        private readonly Dictionary<int, Action> _abilityHandlers = [];

        public float LastHurtOther { get; set; } = 0;

        public virtual void Register() { }

        public void SetDefaultAppearance()
        {
            Player.PlayerPawn.Value.SetColor(GenerateShade(DefaultColor, Player.GetWarcraftPlayer().currentLevel));

            var model = Player.Team == CsTeam.CounterTerrorist ? DefaultModel.CTModel : DefaultModel.TModel;

            if (model != null && model != string.Empty)
            {
                Player.PlayerPawn.Value.SetModel(model);
            }
        }

        private static Color GenerateShade(Color baseColor, int shadeIndex)
        {
            if (shadeIndex < 1 || shadeIndex > 16)
            {
                return Color.White;
            }

            // Convert 1-based index to 0-based index
            int i = shadeIndex - 1;

            // Calculate the blend ratio
            double ratio = i / 15.0;

            // Interpolate between white and the base color
            int r = (int)(255 * (1 - ratio) + baseColor.R * ratio);
            int g = (int)(255 * (1 - ratio) + baseColor.G * ratio);
            int b = (int)(255 * (1 - ratio) + baseColor.B * ratio);

            // Ensure the values are within the valid range
            r = Math.Clamp(r, 0, 255);
            g = Math.Clamp(g, 0, 255);
            b = Math.Clamp(b, 0, 255);

            return Color.FromArgb(r, g, b);
        }

        public IWarcraftAbility GetAbility(int index)
        {
            return _abilities[index];
        }

        protected void AddAbility(IWarcraftAbility ability)
        {
            _abilities.Add(ability);
        }

        protected void HookEvent<T>(string eventName, Action<T> handler) where T : GameEvent
        {
            _eventHandlers[eventName] = (evt) =>
            {
                if (evt is T typedEvent)
                {
                    handler(typedEvent);
                }
                else
                {
                    handler((T)evt);
                    Console.WriteLine($"Handler for event {eventName} expects an event of type {typeof(T).Name}.");
                }
            };
        }

        protected void HookAbility(int abilityIndex, Action handler)
        {
            _abilityHandlers[abilityIndex] = handler;
        }

        public void InvokeEvent(string eventName, GameEvent @event)
        {
            if (_eventHandlers.TryGetValue(eventName, out Action<GameEvent> value))
            {
                value.Invoke(@event);
            }
        }

        public virtual void PlayerChangingToAnotherRace()
        {
            Player.PlayerPawn.Value.SetColor(Color.White);
        }

        public bool IsAbilityReady(int abilityIndex)
        {
            return Cooldowns.CooldownManager.IsAvailable(WarcraftPlayer, abilityIndex);
        }

        public float AbilityCooldownRemaining(int abilityIndex)
        {
            return Cooldowns.CooldownManager.Remaining(WarcraftPlayer, abilityIndex);
        }

        public void StartCooldown(int abilityIndex)
        {
            var ability = _abilities[abilityIndex];

            if (ability is WarcraftCooldownAbility cooldownAbility)
                Cooldowns.CooldownManager.StartCooldown(WarcraftPlayer, abilityIndex, cooldownAbility.Cooldown);
        }

        public void InvokeAbility(int abilityIndex)
        {
            if (_abilityHandlers.TryGetValue(abilityIndex, out Action value))
            {
                value.Invoke();
            }
        }

        public static void DispatchEffect(WarcraftEffect effect)
        {
            WarcraftPlugin.Instance.EffectManager.AddEffect(effect);
        }

        internal void ResetCooldowns()
        {
            Cooldowns.CooldownManager.ResetCooldowns(WarcraftPlayer);
        }
    }
}