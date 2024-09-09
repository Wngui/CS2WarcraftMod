using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftPlugin.Classes;

namespace WarcraftPlugin
{
    public class RaceManager
    {
        private readonly Dictionary<string, Type> _races = [];
        private readonly Dictionary<string, WarcraftClass> _raceObjects = [];

        public void Initialize()
        {
            RegisterRace<Mage>();
            RegisterRace<Rogue>();
            RegisterRace<Barbarian>();
            RegisterRace<Ranger>();
            RegisterRace<Paladin>();
            RegisterRace<Necromancer>();
            RegisterRace<Shapeshifter>();
            RegisterRace<Tinker>();
        }

        private void RegisterRace<T>() where T : WarcraftClass, new()
        {
            var race = new T();
            race.Register();
            _races[race.InternalName] = typeof(T);
            _raceObjects[race.InternalName] = race;
        }

        public WarcraftClass InstantiateClass(string name)
        {
            if (!_races.ContainsKey(name)) throw new Exception("Race not found: " + name);

            var race = (WarcraftClass)Activator.CreateInstance(_races[name]);
            race.Register();

            return race;
        }

        public WarcraftClass[] GetAllClasses()
        {
            return _raceObjects.Values.ToArray();
        }

        public WarcraftClass GetRace(string name)
        {
            return _raceObjects.TryGetValue(name, out WarcraftClass value) ? value : null;
        }
    }
}