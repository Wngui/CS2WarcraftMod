using CounterStrikeSharp.API.Core;
using System.Collections.Generic;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Models
{
    internal class WarcraftPlayer
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

        private readonly List<int> _abilityLevels = new(new int[4]);
        internal List<float> AbilityCooldowns = new(new float[4]);

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

        internal int GetLevel()
        {
            if (currentLevel > WarcraftPlugin.MaxLevel) return WarcraftPlugin.MaxLevel;

            return currentLevel;
        }

        internal int GetAbilityLevel(int abilityIndex)
        {
            return _abilityLevels[abilityIndex];
        }

        internal static int GetMaxAbilityLevel(int abilityIndex)
        {
            return abilityIndex == 3 ? 1 : WarcraftPlugin.MaxSkillLevel;
        }

        internal void SetAbilityLevel(int abilityIndex, int value)
        {
            _abilityLevels[abilityIndex] = value;
        }

        internal WarcraftClass GetClass()
        {
            return _class;
        }

        internal void GrantAbilityLevel(int abilityIndex)
        {
            Player.PlayLocalSound("sounds/buttons/button9.vsnd");
            _abilityLevels[abilityIndex] += 1;
        }
    }
}
