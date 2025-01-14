using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using WarcraftPlugin.Models;


namespace WarcraftPlugin.Core
{
    public class ClassManager
    {
        private readonly Dictionary<string, Type> _classes = [];
        private readonly Dictionary<string, WarcraftClass> _classObjects = [];

        public void Initialize(string moduleDirectory)
        {
            // 1. Register classes from the main assembly (already loaded).
            RegisterClassesFromAssembly(Assembly.GetExecutingAssembly());

            // 2. Register custom classes from dynamically compiled hero files.
            var customfolderPath = CreateCustomHeroesFolder(moduleDirectory);
            var customHeroFiles = Directory.GetFiles(customfolderPath, "*.cs");

            // Compile and register custom hero classes
            if (customHeroFiles.Length > 0)
            {
                var assembly = CompileCustomHeroes();
                //var compiledAssembly = CompileCustomHeroes(customHeroFiles);
                RegisterClassesFromAssembly(assembly);
            }
        }

        private static string CreateCustomHeroesFolder(string moduleDirectory)
        {
            var customHeroesFolder = Path.Combine(moduleDirectory, "CustomHeroes");
            if (!Directory.Exists(customHeroesFolder))
            {
                Directory.CreateDirectory(customHeroesFolder);
            }
            return customHeroesFolder;
        }

        static Assembly CompileCustomHeroes()
        {
            string csharpFilePath = @"C:\cs2-server\cs2-ds\game\csgo\addons\counterstrikesharp\plugins\WarcraftPlugin\CustomHeroes\TestDude.cs";
            string referencesFolderPath = @"C:\cs2-server\cs2-ds\game\csgo\addons\counterstrikesharp\plugins\WarcraftPlugin";

            // Step 1: Compile the C# file
            string compiledAssemblyPath = CompileCSharpFile(csharpFilePath, referencesFolderPath);

            if (compiledAssemblyPath != null)
            {
                // Step 2: Load the compiled assembly
                Assembly assembly = Assembly.LoadFrom(compiledAssemblyPath);
                return assembly;
            }

            return null;
        }

        static string CompileCSharpFile(string csharpFilePath, string referencesFolderPath)
        {
            // Read the C# file content
            string code = File.ReadAllText(csharpFilePath);

            // Parse the C# code into a syntax tree
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            // References for the compilation (add all DLLs in the specified folder)
            var references = Directory.GetFiles(referencesFolderPath, "*.dll")
                                       .Select(dll => MetadataReference.CreateFromFile(dll))
                                       .ToList();

            references.AddRange(Directory.GetFiles("C:\\cs2-server\\cs2-ds\\game\\csgo\\addons\\counterstrikesharp\\dotnet\\shared\\Microsoft.NETCore.App\\8.0.3", "*.dll")
                               .Where(dll =>
                               {
                                   try
                                   {
                                       // Attempt to load the assembly and check if it's a valid managed assembly
                                       var assembly = Assembly.LoadFrom(dll);
                                       return true; // It's a managed assembly
                                   }
                                   catch
                                   {
                                       return false; // It's a native assembly (non-managed)
                                   }
                               })
                               .Select(dll => MetadataReference.CreateFromFile(dll))
                               .ToList());

            // Add the core references needed by Roslyn
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location)); // Core assembly (mscorlib)
            references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)); // System assembly (System.dll)

            // Compilation options
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            // Create the compilation
            var compilation = CSharpCompilation.Create(
                "DynamicAssembly",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: compilationOptions);

            // Output path
            string outputPath = Path.Combine(Path.GetTempPath(), "DynamicAssembly.dll");

            // Emit the assembly to the output path
            EmitResult result = compilation.Emit(outputPath);

            if (!result.Success)
            {
                // Compilation failed, print errors
                foreach (var diagnostic in result.Diagnostics)
                {
                    Console.WriteLine(diagnostic.ToString());
                }
                return null;
            }

            Console.WriteLine("Compilation successful!");
            return outputPath;
        }

        private void RegisterClassesFromAssembly(Assembly assembly)
        {
            Console.WriteLine($"Registering classes from assembly: {assembly.FullName}");
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
            if (!typeof(WarcraftClass).IsAssignableFrom(type))
                throw new ArgumentException("Type must inherit from WarcraftClass");

            Console.WriteLine($"Registering class: {type.Name}");

            var instance = Activator.CreateInstance(type) as WarcraftClass;
            instance?.Register();

            _classes[instance.InternalName] = type;
            _classObjects[instance.InternalName] = instance;
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