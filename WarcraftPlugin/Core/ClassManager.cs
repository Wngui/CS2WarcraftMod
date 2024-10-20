using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftPlugin.Classes;

namespace WarcraftPlugin.Core
{
    public class ClassManager
    {
        private readonly Dictionary<string, Type> _classes = [];
        private readonly Dictionary<string, WarcraftClass> _classObjects = [];

        public void Initialize()
        {
            RegisterClass<Mage>();
            RegisterClass<Rogue>();
            RegisterClass<Barbarian>();
            RegisterClass<Ranger>();
            RegisterClass<Paladin>();
            RegisterClass<Necromancer>();
            RegisterClass<Shapeshifter>();
            RegisterClass<Tinker>();
        }

        private void RegisterClass<T>() where T : WarcraftClass, new()
        {
            var race = new T();
            race.Register();
            _classes[race.InternalName] = typeof(T);
            _classObjects[race.InternalName] = race;
        }

        public WarcraftClass InstantiateClass(string name)
        {
            if (!_classes.ContainsKey(name)) throw new Exception("Race not found: " + name);

            var race = (WarcraftClass)Activator.CreateInstance(_classes[name]);
            race.Register();

            return race;
        }

        public WarcraftClass[] GetAllClasses()
        {
            return _classObjects.Values.ToArray();
        }

        public WarcraftClass GetRace(string name)
        {
            return _classObjects.TryGetValue(name, out WarcraftClass value) ? value : null;
        }
    }
}