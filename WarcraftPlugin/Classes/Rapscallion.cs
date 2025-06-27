using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftPlugin.Core.Effects;
using WarcraftPlugin.Events.ExtendedEvents;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Models;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace WarcraftPlugin.Classes
{
    public class Rapscallion : WarcraftClass
    {
        private readonly int _MedkitHealthMultiplier = 20;
        private bool UltimateToggle = false;
        private Dictionary<ulong, ChargeWhileMovingEffect> _chargeEffects = new();
        private RestrictWeaponsEffect? _ultWeaponLock;
        private FlashingInvisibilityEffect? _flashEffect;
        private bool _hadBombWhenUlted = false;

        public override string DisplayName => "Rapscallion";
        public override Color DefaultColor => Color.White;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Ninja skills", "Heal to 120/140/160/180/200 HP when planting or defusing a bomb."),
            new WarcraftAbility("Agility", "Builds charge while moving for up to 40% evasion and 160% speed; higher levels charge faster."),
            new WarcraftAbility("Unseen Blade", "Additional 12/24/36/48/60 knife damage."),
            new WarcraftCooldownAbility("Vanish", "Full Invisiblity toggle", 2f)
        ];

        public override void Register()
        {
            HookEvent<EventBombBeginplant>(BombBeginPlant);
            HookEvent<EventBombBegindefuse>(BombBeginDefuse);
            HookEvent<EventPlayerSpawn>(PlayerSpawn);
            HookEvent<EventPlayerHurtOther>(PlayerhurtOther);
            HookEvent<EventPlayerHurt>(PlayerHurt);

            HookAbility(3, Ultimate);
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            WarcraftPlugin.Instance.AddTimer(0.2f, () =>
            {
                if (Player == null || !Player.IsValid || !Player.IsAlive()) return;

                if (_chargeEffects.TryGetValue(Player.SteamID, out var existingEffect))
                {
                    existingEffect.Destroy();
                }

                int abilityLevel = WarcraftPlayer.GetAbilityLevel(2);

                var effect = new ChargeWhileMovingEffect(Player, abilityLevel);
                _chargeEffects[Player.SteamID] = effect;
                UltimateToggle = false;
                var allowedWeapons = new List<string> { "weapon_knife" };

                if (Player.TeamNum == (int)CsTeam.Terrorist)
                {
                    allowedWeapons.Add("weapon_c4");
                    new RestrictWeaponsEffect(Player, 999f, allowedWeapons, includeBomb: true).Start();
                }
                else
                {
                    new RestrictWeaponsEffect(Player, 999f, allowedWeapons, includeBomb: false).Start();
                }


                effect.Start();

                _flashEffect?.Destroy();
                _flashEffect = new FlashingInvisibilityEffect(Player);
                _flashEffect.Start();

            });

        }

        private void PlayerHurt(EventPlayerHurt @event)
        {
            var victim = @event.Userid;
            var attacker = @event.Attacker;
            if (attacker == null || victim == null || !attacker.IsValid || !victim.IsValid) return;

            if (!_chargeEffects.TryGetValue(Player.SteamID, out var chargeEffect))
                return;

            int chargeStacks = chargeEffect.ChargeStacks;
            float evasionChance = Math.Clamp((chargeStacks / 100f) * 0.4f, 0f, 0.4f);

            if (Random.Shared.NextDouble() < evasionChance)
            {
                @event.IgnoreDamage();
                victim.PrintToCenter($" {ChatColors.Green}⚡ You evaded incoming damage!");
            }
        }

        private void PlayerhurtOther(EventPlayerHurtOther @event)
        {
            var pawn = Player.PlayerPawn.Value;
            var victim = @event.Userid;
            var attacker = @event.Attacker;
            int abilityLevel = WarcraftPlayer.GetAbilityLevel(2);

            if (!attacker.IsValid || !victim.IsValid || !victim.IsAlive()) return;

            if (attacker.TeamNum == victim.TeamNum)
                return;


            var activeWeaponName = pawn.WeaponServices!.ActiveWeapon.Value.DesignerName;
            if (activeWeaponName == "weapon_knife")
            {
                var damageBonus = WarcraftPlayer.GetAbilityLevel(2) * 12;
                @event.AddBonusDamage(damageBonus, abilityName: GetAbility(2).DisplayName);
            }
        }

        private void HandleBombEvent(CCSPlayerController player)
        {
            if (player == null || player.PlayerPawn == null || player.PlayerPawn.Value == null)
            {
                Console.WriteLine("ERROR: Player is NULL! Maybe they haven't spawned yet?");
                return;
            }
            int abilityLevel = WarcraftPlayer.GetAbilityLevel(0);

            int newHealth = 100 + abilityLevel * _MedkitHealthMultiplier;
            player.SetHp(newHealth);
            player.PlayerPawn.Value.MaxHealth = player.PlayerPawn.Value.Health;
            var smoke = Warcraft.SpawnSmoke(player.PlayerPawn.Value.AbsOrigin.Clone().Add(z: 5), player.PlayerPawn.Value, Color.Black);
            smoke.SpawnTime = 0;
            smoke.Teleport(velocity: Vector.Zero);
        }

        private void BombBeginPlant(EventBombBeginplant @event)
        {
            HandleBombEvent(Player);
        }

        private void BombBeginDefuse(EventBombBegindefuse @event)
        {
            HandleBombEvent(Player);
        }

        private static void SetMoveType(CCSPlayerPawn pawn, MoveType_t moveType)
        {
            if (pawn == null) return;
            pawn.MoveType = moveType;
            pawn.ActualMoveType = moveType;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
        }

        private bool HasWeapon(CCSPlayerController player, string weaponName)
        {
            var pawn = player.PlayerPawn?.Value;
            if (pawn?.WeaponServices?.MyWeapons == null) return false;

            return pawn.WeaponServices.MyWeapons.Any(w => w.Value?.DesignerName == weaponName);
        }


        private void Ultimate()
        {
            var pawn = Player.PlayerPawn.Value;
            if (pawn == null || !Player.IsAlive()) return;

            var itemServices = new CCSPlayer_ItemServices(Player.PlayerPawn.Value.ItemServices.Handle);
            StartCooldown(3);

            if (UltimateToggle)
            {
                SetMoveType(pawn, MoveType_t.MOVETYPE_WALK);
                pawn.Teleport(null, null, new Vector(0, 0, 0));
                UltimateToggle = false;

                Player.PrintToChat($" {ChatColors.Green}Visible again.");
                Player.PlayerPawn.Value.SetColor(Color.FromArgb(255, 255, 255, 255));
                if (Player.TeamNum == (int)CsTeam.CounterTerrorist)
                {
                    itemServices.HasDefuser = true;
                }

                List<string> restoreWeapons = new() { "weapon_knife" };
                if (Player.TeamNum == (int)CsTeam.Terrorist)
                {
                    restoreWeapons.Add("weapon_c4");
                }

                _ultWeaponLock?.Destroy();
                _flashEffect?.Destroy();

                _flashEffect = new FlashingInvisibilityEffect(Player);
                _flashEffect.Start();

                _ultWeaponLock = new RestrictWeaponsEffect(Player, 999f, restoreWeapons, _hadBombWhenUlted);
                _ultWeaponLock.Start();

                _hadBombWhenUlted = false;
            }
            else
            {
                UltimateToggle = true;
                SetMoveType(pawn, MoveType_t.MOVETYPE_FLY);

                Player.PrintToChat($" {ChatColors.Green}You are now frozen invisible!");
                _flashEffect?.Destroy();
                Player.PlayerPawn.Value.SetColor(Color.FromArgb(0, 255, 255, 255));

                _hadBombWhenUlted = HasWeapon(Player, "weapon_c4");

                itemServices.HasDefuser = false;
                DeleteAllWeapons(Player);


                var noWeapons = new List<string>();
                _ultWeaponLock?.Destroy();
                _ultWeaponLock = new RestrictWeaponsEffect(Player, 999f, noWeapons);
                _ultWeaponLock.Start();
            }
        }


        internal class FlashingInvisibilityEffect : WarcraftEffect
        {
            private bool _isInvisible = false;

            public FlashingInvisibilityEffect(CCSPlayerController owner)
                : base(owner, duration: 999f, destroyOnDeath: true, destroyOnRoundEnd: true, onTickInterval: 0.5f)
            {
            }

            public override void OnStart()
            {
                SetInvisibility(true);
            }

            public override void OnTick()
            {
                _isInvisible = !_isInvisible;
                SetInvisibility(_isInvisible);
            }

            public override void OnFinish()
            {
                SetInvisibility(false);
            }

            private void SetInvisibility(bool invisible)
            {
                if (Owner?.PlayerPawn?.Value == null) return;

                var color = invisible
                    ? Color.FromArgb(0, 255, 255, 255)
                    : Color.FromArgb(255, 255, 255, 255);

                Owner.PlayerPawn.Value.SetColor(color);
            }
        }

        private class RestrictWeaponsEffect : WarcraftEffect
        {
            private readonly List<string> _allowedWeapons;
            private readonly bool _includeBomb;


            public RestrictWeaponsEffect(CCSPlayerController owner, float duration, List<string> allowedWeapons, bool includeBomb = false)
    : base(owner, duration)
            {
                _allowedWeapons = allowedWeapons;
                _includeBomb = includeBomb;
            }

            public override void OnStart()
            {
                if (!Owner.IsValid || Owner.PlayerPawn?.Value == null)
                    return;

                WarcraftPlugin.Instance.AddTimer(0.3f, () =>
                {
                    DropAllWeaponsExceptAllowed();

                    WarcraftPlugin.Instance.AddTimer(0.2f, () =>
                    {
                        GiveAllowedWeapons();
                    });
                });
            }

            public override void OnTick()
            {
                if (!Owner.IsValid || Owner.PlayerPawn?.Value == null)
                    return;

                var activeWeapon = Owner.PlayerPawn.Value.WeaponServices?.ActiveWeapon?.Value;
                var weaponName = activeWeapon?.DesignerName;

                if (string.IsNullOrEmpty(weaponName) || _allowedWeapons.Contains(weaponName))
                    return;

                Console.WriteLine($"[RestrictWeaponsEffect] Disallowed weapon detected: {weaponName}");

                DropAllWeaponsExceptAllowed();

                WarcraftPlugin.Instance.AddTimer(0.2f, () =>
                {
                    GiveAllowedWeapons();
                });
            }


            public override void OnFinish()
            {
                if (!Owner.IsValid || Owner.PlayerPawn?.Value == null)
                    return;
            }

            private void DropAllWeaponsExceptAllowed()
            {
                var pawn = Owner.PlayerPawn.Value;
                var weapons = pawn.WeaponServices.MyWeapons;
                if (weapons == null) return;

                for (int i = weapons.Count - 1; i >= 0; i--)
                {
                    var weapon = weapons[i].Value;
                    if (weapon == null || !_allowedWeapons.Contains(weapon.DesignerName))
                    {
                        DropWeaponByDesignerName(Owner, weapon?.DesignerName ?? "");
                    }
                }
            }

            private void DropWeaponByDesignerName(CCSPlayerController player, string weaponName)
            {
                if (player == null || !player.IsValid || string.IsNullOrEmpty(weaponName)) return;

                var pawn = player.PlayerPawn.Value;
                if (pawn == null || !pawn.IsValid || !player.PawnIsAlive || pawn.WeaponServices == null) return;

                var matchedWeapon = pawn.WeaponServices.MyWeapons
                    .FirstOrDefault(x => x.Value?.DesignerName == weaponName);

                if (matchedWeapon != null && matchedWeapon.IsValid)
                {
                    pawn.WeaponServices.ActiveWeapon.Raw = matchedWeapon.Raw;
                    player.DropActiveWeapon();
                }
            }

            private void GiveAllowedWeapons()
            {
                var pawn = Owner.PlayerPawn.Value;
                var inventory = pawn.WeaponServices.MyWeapons;

                foreach (var weaponName in _allowedWeapons)
                {
                    // Only give C4 if _includeBomb is true and the player is a terrorist
                    if (weaponName == "weapon_c4" && (!_includeBomb || Owner.TeamNum != (int)CsTeam.Terrorist))
                        continue;

                    bool alreadyHasWeapon = inventory.Any(w => w.Value?.DesignerName == weaponName);

                    if (!alreadyHasWeapon)
                    {
                        Owner.GiveNamedItem(weaponName);
                    }
                }
            }

        }

        public static void DeleteAllWeapons(CCSPlayerController player)
        {
            if (player == null) return;
            player.DropWeaponByDesignerName("weapon_c4");
            player.RemoveWeapons();
        }

        internal class ChargeWhileMovingEffect : WarcraftEffect
        {
            private float _lastChatTime;
            public int _chargeStacks;
            private Vector _lastPosition;
            private readonly int _maxCharge = 200;
            private readonly int _abilityLevel;

            public int ChargeStacks => _chargeStacks;

            public ChargeWhileMovingEffect(CCSPlayerController owner, int abilityLevel)
        : base(owner, duration: 9999f, destroyOnDeath: true, destroyOnRoundEnd: true, onTickInterval: 1.8f)
            {
                _abilityLevel = abilityLevel;
            }

            private void CheckForMovementAndAddCharge()
            {
                if (Owner == null || Owner.PlayerPawn?.Value == null || !Owner.IsAlive())
                    return;

                var currentPosition = CopyPosition(Owner.PlayerPawn.Value.AbsOrigin);
                var diff = currentPosition - _lastPosition;
                float movedDist = diff.X * diff.X + diff.Y * diff.Y + diff.Z * diff.Z;

                bool isMoving = movedDist > 4f;
                if (!isMoving)
                {
                    _lastPosition = currentPosition;
                    return;
                }
                _chargeStacks = Math.Min(_chargeStacks + _abilityLevel, _maxCharge);

                float now = Server.CurrentTime;
                if (now - _lastChatTime > 2f)
                {
                    Owner.PrintToCenter($"Charge: {_chargeStacks}/200");
                    _lastChatTime = now;
                }

                int tier = Math.Min(_chargeStacks / 20, 10);
                float buffMultiplier = tier * 0.1f;
                float newSpeed = 1.0f + (buffMultiplier / 2f);
                Owner.PlayerPawn.Value.VelocityModifier = Math.Min(newSpeed, 1.6f);


                _lastPosition = currentPosition;
            }

            public override void OnStart()
            {
                if (Owner?.PlayerPawn?.Value == null) return;
                _lastPosition = CopyPosition(Owner.PlayerPawn.Value.AbsOrigin);
            }

            public override void OnTick()
            {
                try
                {
                    CheckForMovementAndAddCharge();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChargeSystem] OnTick crashed: {ex.Message}");
                }
            }

            public override void OnFinish()
            {
                if (Owner?.IsValid == true)
                {
                    Owner.PrintToChat($" {ChatColors.Green}[ChargeSystem]{ChatColors.Default} Stopped charging.");
                }
                _chargeStacks = 0;
            }

            private Vector CopyPosition(Vector pos)
            {
                return new Vector(pos.X, pos.Y, pos.Z);
            }
        }
    }
}
