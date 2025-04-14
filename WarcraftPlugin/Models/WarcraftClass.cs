using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using System.Drawing;
using WarcraftPlugin.Helpers;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Core;
using System.Linq;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Localization;
using WarcraftPlugin.lang;

namespace WarcraftPlugin.Models
{
    public interface IWarcraftAbility
    {
        public string InternalName { get; }
        public string DisplayName { get; }
        public string Description { get; }
    }

    public class WarcraftAbility : IWarcraftAbility
    {
        public WarcraftAbility(string displayName, string description)
        {
            DisplayName = displayName;
            Description = description;
        }

        public string InternalName => DisplayName.Replace(' ', '_').ToLowerInvariant();
        public string DisplayName { get; }
        public string Description { get; }
    }

    public class WarcraftCooldownAbility : WarcraftAbility
    {
        public float Cooldown { get; set; } = 0f;

        public WarcraftCooldownAbility(string displayName, string description,
            float cooldown) : base(displayName, description)
        {
            Cooldown = cooldown;
        }
    }

    public abstract class WarcraftClass
    {
        public string InternalName => DisplayName.Replace(' ', '_').ToLowerInvariant();
        public abstract string DisplayName { get; }
        public string LocalizedDisplayName => Localizer.Exists(InternalName) ? Localizer[InternalName] : DisplayName;
        public virtual DefaultClassModel DefaultModel { get; } = new DefaultClassModel();
        public abstract Color DefaultColor { get; }
        public WarcraftPlayer WarcraftPlayer { get; set; }
        public CCSPlayerController Player { get; set; }

        public abstract List<IWarcraftAbility> Abilities { get; }
        private readonly Dictionary<string, GameAction> _eventHandlers = [];
        private readonly Dictionary<int, Action> _abilityHandlers = [];

        private float _killFeedIconTick;
        private KillFeedIcon? _killFeedIcon;
        public float LastHurtOther { get; set; } = 0;

        public abstract void Register();

        public virtual void PlayerChangingToAnotherRace() { SetDefaultAppearance(); }

        public virtual List<string> PreloadResources { get; } = [];
        public readonly IStringLocalizer Localizer = WarcraftPlugin.Instance.Localizer;

        public void SetDefaultAppearance()
        {
            Player.PlayerPawn.Value.SetColor(GenerateShade(DefaultColor, Player.GetWarcraftPlayer().currentLevel));

            var model = Player.Team == CsTeam.CounterTerrorist ? DefaultModel?.CTModel : DefaultModel?.TModel;

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
            var ability = Abilities[index];
            var abilityNameKey = $"{InternalName}.ability.{index}";
            var abilityDescriptionKey = $"{InternalName}.ability.{index}.description";

            return new WarcraftAbility(
                Localizer.Exists(abilityNameKey) ? Localizer[abilityNameKey] : ability.DisplayName,
                Localizer.Exists(abilityDescriptionKey) ? Localizer[abilityDescriptionKey] : ability.Description
            );
        }

        protected void HookEvent<T>(Action<T> handler, HookMode hookMode = HookMode.Pre) where T : GameEvent
        {
            _eventHandlers[typeof(T).Name + (hookMode == HookMode.Pre ? "-pre" : "")] = new GameAction
            {
                EventType = typeof(T),
                Handler = (evt) =>
                {
                    if (evt is T typedEvent)
                    {
                        handler(typedEvent);
                    }
                    else
                    {
                        handler((T)evt);
                        Console.WriteLine($"Handler for event expects an event of type {typeof(T).Name}.");
                    }
                },
                HookMode = hookMode
            };
        }

        protected void HookAbility(int abilityIndex, Action handler)
        {
            _abilityHandlers[abilityIndex] = handler;
        }

        public void InvokeEvent(GameEvent @event, HookMode hookMode = HookMode.Post)
        {
            //Console.WriteLine($"Invoking event {@event.GetType().Name + (hookMode == HookMode.Pre ? "-pre" : "")}");
            if (_eventHandlers.TryGetValue(@event.GetType().Name + (hookMode == HookMode.Pre ? "-pre" : ""), out GameAction gameAction))
            {
                gameAction.Handler.Invoke(@event);
            }
        }

        internal List<GameAction> GetEventListeners()
        {
            return _eventHandlers.Values.ToList();
        }

        public bool IsAbilityReady(int abilityIndex)
        {
            return CooldownManager.IsAvailable(WarcraftPlayer, abilityIndex);
        }

        public float AbilityCooldownRemaining(int abilityIndex)
        {
            return CooldownManager.Remaining(WarcraftPlayer, abilityIndex);
        }

        public void StartCooldown(int abilityIndex, float? customCooldown = null)
        {
            var ability = Abilities[abilityIndex];

            if (ability is WarcraftCooldownAbility cooldownAbility)
                CooldownManager.StartCooldown(WarcraftPlayer, abilityIndex, customCooldown ?? cooldownAbility.Cooldown);
        }

        public void InvokeAbility(int abilityIndex)
        {
            if (_abilityHandlers.TryGetValue(abilityIndex, out Action value))
            {
                value.Invoke();
            }
        }

        public void ResetCooldowns()
        {
            CooldownManager.ResetCooldowns(WarcraftPlayer);
        }

        public void SetKillFeedIcon(KillFeedIcon? damageType)
        {
            _killFeedIconTick = Server.CurrentTime;
            _killFeedIcon = damageType;
        }

        public KillFeedIcon? GetKillFeedIcon()
        {
            return _killFeedIcon;
        }

        public void ResetKillFeedIcon()
        {
            _killFeedIcon = null;
        }

        public float GetKillFeedTick()
        {
            return _killFeedIconTick;
        }
    }
}