using CounterStrikeSharp.API.Core;
using System.Collections.Generic;
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

        private readonly List<int> _abilityLevels = [.. new int[4]];
        internal List<float> AbilityCooldowns = [.. new float[4]];

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

            _abilityLevels[0] = dbRace.Ability1Level;
            _abilityLevels[1] = dbRace.Ability2Level;
            _abilityLevels[2] = dbRace.Ability3Level;
            _abilityLevels[3] = dbRace.Ability4Level;

            _class = WarcraftPlugin.Instance.classManager.InstantiateClassByName(className);
            _class.WarcraftPlayer = this;
            _class.Player = Player;
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
    }
}
