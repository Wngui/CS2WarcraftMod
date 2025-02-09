using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WarcraftPlugin.Compiler;
using WarcraftPlugin.Models;


namespace WarcraftPlugin.Core
{
    internal class ClassManager
    {
        private Dictionary<string, Type> _classes = [];
        private Dictionary<string, WarcraftClass> _classObjects = [];

        private DirectoryInfo _customHeroesFolder;
        private long _customHeroesFilesTimestamp;
        private bool _checkingCustomHeroFiles;
        private Config _config;

        internal void Initialize(string moduleDirectory, Config config)
        {
            _config = config;
            RegisterDefaultClasses();

            _customHeroesFolder = Directory.CreateDirectory(Path.Combine(moduleDirectory, "CustomHeroes"));
            RegisterCustomClasses();
            TrackCustomClassesChanges();
        }

        private void RegisterCustomClasses()
        {
            var customHeroFiles = Directory.GetFiles(_customHeroesFolder.FullName, "*.cs");
            _customHeroesFilesTimestamp = GetLatestTimestamp(customHeroFiles);

            if (customHeroFiles.Length > 0)
            {
                var assembly = CustomHero.CompileAndLoadAssemblies(customHeroFiles);
                RegisterClasses(assembly);
            }
        }

        private void RegisterDefaultClasses()
        {
            RegisterClasses(Assembly.GetExecutingAssembly());
        }

        private void RegisterClasses(Assembly assembly)
        {
            var heroClasses = assembly.GetTypes()
                .Where(t =>
                    t.Namespace == "WarcraftPlugin.Classes" && // Ensure it’s in the right namespace
                    t.IsClass &&                             // Ensure it's a class
                    !t.IsAbstract &&                         // Exclude abstract classes
                    typeof(WarcraftClass).IsAssignableFrom(t) // Ensure it derives from WarcraftClass
                );

            foreach (var heroClass in heroClasses)
            {
                RegisterClass(heroClass);
            }
        }

        private void RegisterClass(Type type)
        {
            if (_config.DeactivatedClasses.Contains(type.Name, StringComparer.InvariantCultureIgnoreCase))
            {
                Console.WriteLine($"Skipping deactivated class: {type.Name}");
                return;
            }

            WarcraftClass heroClass;
            try
            {
                heroClass = InstantiateClass(type);
                heroClass.Register();
            }
            catch (Exception)
            {
                Console.WriteLine($"Error registering class {type.Name}");
                throw;
            }

            if (_config.DeactivatedClasses.Contains(heroClass.InternalName, StringComparer.InvariantCultureIgnoreCase) ||
                _config.DeactivatedClasses.Contains(heroClass.DisplayName, StringComparer.InvariantCultureIgnoreCase))
            {
                Console.WriteLine($"Skipping deactivated class: {heroClass.DisplayName}");
                return;
            }

            Console.WriteLine($"Registered class: {heroClass.DisplayName}");
            _classes[heroClass.InternalName] = type;
            _classObjects[heroClass.InternalName] = heroClass;
        }

        internal WarcraftClass InstantiateClassByName(string name)
        {
            if (!_classes.ContainsKey(name)) throw new Exception("Race not found: " + name);

            var heroClass = InstantiateClass(_classes[name]);
            heroClass.Register();

            return heroClass;
        }

        internal static WarcraftClass InstantiateClass(Type type)
        {
            WarcraftClass heroClass;
            heroClass = (WarcraftClass)Activator.CreateInstance(type);

            return heroClass;
        }

        private void TrackCustomClassesChanges()
        {
            WarcraftPlugin.Instance.AddTimer(5, () =>
            {
                if (_checkingCustomHeroFiles) return;
                _checkingCustomHeroFiles = true;

                try
                {
                    var customHeroFiles = Directory.GetFiles(_customHeroesFolder.FullName, "*.cs");
                    if (_customHeroesFilesTimestamp != GetLatestTimestamp(customHeroFiles))
                    {
                        Console.WriteLine("Reloading custom hero files...");
                        _classes.Clear();
                        _classObjects.Clear();
                        CustomHero.UnloadAssembly();

                        //Trigger plugin reload
                        File.SetLastWriteTime(Path.Combine(_customHeroesFolder.Parent.FullName, "WarcraftPlugin.dll"), DateTime.Now);
                    }
                }
                finally
                {
                    _checkingCustomHeroFiles = false;
                }
            }, TimerFlags.REPEAT);
        }

        internal WarcraftClass[] GetAllClasses()
        {
            return _classObjects.Values.ToArray();
        }

        internal WarcraftClass GetDefaultClass()
        {
            var defaultClass = _classObjects.Values.FirstOrDefault();
            return defaultClass ?? throw new Exception("No warcraft classes registered!!!");
        }

        private static long GetLatestTimestamp(string[] files)
        {
            return files.Select(file => File.GetLastWriteTime(file).Ticks).Max();
        }
    }
}