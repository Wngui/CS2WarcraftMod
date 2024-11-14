using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using System.Collections.Generic;
using WarcraftPlugin.Core;
using WarcraftPlugin.Helpers;

namespace WarcraftPlugin.Models
{
    public class WarcraftPlayer
    {
        private int _playerIndex;
        public int Index => _playerIndex;
        public bool IsMaxLevel => currentLevel == WarcraftPlugin.MaxLevel;
        public CCSPlayerController GetPlayer() => Player;

        public CCSPlayerController Player { get; init; }

        public string DesiredClass { get; set; }

        public int currentXp;
        public int currentLevel;
        public int amountToLevel;
        public string className;
        public string statusMessage;

        private readonly List<int> _abilityLevels = new(new int[4]);
        public List<float> AbilityCooldowns = new(new float[4]);

        private WarcraftClass _class;

        public WarcraftPlayer(CCSPlayerController player)
        {
            Player = player;
        }

        public void LoadFromDatabase(DatabaseClassInformation dbRace, XpSystem xpSystem)
        {
            currentLevel = dbRace.CurrentLevel;
            currentXp = dbRace.CurrentXp;
            className = dbRace.RaceName;
            amountToLevel = xpSystem.GetXpForLevel(currentLevel);

            _abilityLevels[0] = dbRace.Ability1Level;
            _abilityLevels[1] = dbRace.Ability2Level;
            _abilityLevels[2] = dbRace.Ability3Level;
            _abilityLevels[3] = dbRace.Ability4Level;

            _class = WarcraftPlugin.Instance.classManager.InstantiateClass(className);
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
            return _abilityLevels[abilityIndex];
        }

        public static int GetMaxAbilityLevel(int abilityIndex)
        {
            return abilityIndex == 3 ? 1 : WarcraftPlugin.MaxSkillLevel;
        }

        public void SetAbilityLevel(int abilityIndex, int value)
        {
            _abilityLevels[abilityIndex] = value;
        }

        public WarcraftClass GetClass()
        {
            return _class;
        }

        public void SetStatusMessage(string status, float duration = 2f)
        {
            statusMessage = status;
            _ = new Timer(duration, () => statusMessage = null, 0);
            GetPlayer().PrintToChat(" " + status);
        }

        public void GrantAbilityLevel(int abilityIndex)
        {
            Player.PlayLocalSound("sounds/buttons/button9.vsnd");
            _abilityLevels[abilityIndex] += 1;
        }
    }
}
