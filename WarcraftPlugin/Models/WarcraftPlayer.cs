using CounterStrikeSharp.API.Core;
using System.Collections.Generic;
using System.Linq;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;
using WarcraftPlugin.Items;

namespace WarcraftPlugin.Models
{
    public class WarcraftPlayer
    {
        private int _playerIndex;
        internal int Index => _playerIndex;
        internal bool IsMaxLevel => currentLevel == WarcraftPlugin.MaxLevel;
        internal CCSPlayerController GetPlayer() => Player;

        internal CCSPlayerController Player { get; init; }

        internal string DesiredClass { get; set; }

        internal int currentXp;
        internal int currentLevel;
        internal int amountToLevel;
        internal string className;

        internal const int UltimateAbilityIndex = 3;

        private readonly List<int> _abilityLevels = [];
        internal List<float> AbilityCooldowns = [];

        internal readonly List<ShopItem> Items = [];

        private WarcraftClass _class;

        internal WarcraftPlayer(CCSPlayerController player)
        {
            Player = player;
        }

        internal void LoadClassInformation(ClassInformation dbRace, XpSystem xpSystem)
        {
            currentLevel = dbRace.CurrentLevel;
            currentXp = dbRace.CurrentXp;
            className = dbRace.RaceName;
            amountToLevel = xpSystem.GetXpForLevel(currentLevel);

            _class = WarcraftPlugin.Instance.classManager.InstantiateClassByName(className);
            _class.WarcraftPlayer = this;
            _class.Player = Player;

            EnsureAbilityCapacity(_class.Abilities.Count);

            for (int i = 0; i < _class.Abilities.Count; i++)
            {
                _abilityLevels[i] = i < dbRace.AbilityLevels.Count ? dbRace.AbilityLevels[i] : 0;
            }
        }

        public int GetLevel()
        {
            if (currentLevel > WarcraftPlugin.MaxLevel) return WarcraftPlugin.MaxLevel;

            return currentLevel;
        }

        public override string ToString()
        {
            return
                $"[{_playerIndex}]: {{raceName={className}, currentLevel={currentLevel}, currentXp={currentXp}, amountToLevel={amountToLevel}}}";
        }

        public int GetAbilityLevel(int abilityIndex)
        {
            if (abilityIndex >= _abilityLevels.Count)
                return 0;
            return _abilityLevels[abilityIndex];
        }

        public static int GetMaxAbilityLevel(int abilityIndex)
        {
            return abilityIndex == UltimateAbilityIndex ? WarcraftPlugin.MaxUltimateLevel : WarcraftPlugin.MaxSkillLevel;
        }

        public void SetAbilityLevel(int abilityIndex, int value)
        {
            EnsureAbilityCapacity(abilityIndex + 1);
            _abilityLevels[abilityIndex] = value;
        }

        public WarcraftClass GetClass()
        {
            return _class;
        }

        public void GrantAbilityLevel(int abilityIndex)
        {
            Player.PlayLocalSound("sounds/buttons/button9.vsnd");
            _abilityLevels[abilityIndex] += 1;
        }

        private void EnsureAbilityCapacity(int count)
        {
            while (_abilityLevels.Count < count)
            {
                _abilityLevels.Add(0);
            }

            while (AbilityCooldowns.Count < count)
            {
                AbilityCooldowns.Add(0);
            }
        }

        internal bool AddItem(ShopItem item)
        {
            if (Items.Any(inv => inv.GetType() == item.GetType()))
                return false;

            Items.Add(item);
            item.Apply(Player);
            return true;
        }

        internal void ClearItems()
        {
            Items.Clear();
        }
    }
}
