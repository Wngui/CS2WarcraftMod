using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Collections.Generic;
using System.Runtime.Loader;

namespace WarcraftPlugin.Compiler
{
    internal class CustomHero
    {
        internal static Assembly CompileAndLoadAssemblies(string[] heroFiles)
        {
            // Parse the C# code into syntax trees
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var heroFile in heroFiles)
            {
                try
                {
                    var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(heroFile));
                    syntaxTrees.Add(syntaxTree);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading custom hero {heroFile}: {ex.Message}");
                }
            }

            // Add all shared core references
            var references = new List<MetadataReference>();
            references.AddRange(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll")
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

            // Add all loaded assemblies
            foreach (Assembly loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.IsNullOrEmpty(loadedAssembly.Location))
                {
                    // Adding references with location
                    references.Add(MetadataReference.CreateFromFile(loadedAssembly.Location));
                }
                else
                {
                    unsafe
                    {
                        if (loadedAssembly.TryGetRawMetadata(out byte* blob, out int length))
                        {
                            // Add in-memory references
                            var moduleMetadata = ModuleMetadata.CreateFromMetadata((nint)blob, length);
                            var assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
                            var metadataReference = assemblyMetadata.GetReference();
                            references.Add(metadataReference);
                        }
                    }
                }
            }

            // Compilation options
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            // Create the compilation
            var compilation = CSharpCompilation.Create(
                $"W3CustomHeroes-{Guid.NewGuid()}",
                syntaxTrees: syntaxTrees,
                references: references,
                options: compilationOptions);

            // Emit the assembly to a memory stream
            using var memoryStream = new MemoryStream();
            EmitResult result = compilation.Emit(memoryStream);

            if (!result.Success)
            {
                // Compilation failed, print errors
                foreach (var diagnostic in result.Diagnostics)
                {
                    Console.WriteLine(diagnostic.ToString());
                }
                return null;
            }

            Console.WriteLine("Loading custom heroes...");
            memoryStream.Seek(0, SeekOrigin.Begin);
            var customLoadContext = new CustomLoadContext();
            var assembly = customLoadContext.LoadFromStream(memoryStream);

            //Debug info
            //var allTypes = assembly.GetTypes();
            //foreach (var type in allTypes)
            //{
            //    Console.WriteLine($"Type: {type.FullName}, Namespace: {type.Namespace}, IsClass: {type.IsClass}, IsAbstract: {type.IsAbstract}, IsAssignableFrom: {typeof(WarcraftClass).IsAssignableFrom(type)}");
            //}
            return assembly;
        }

        internal static void UnloadAssembly()
        {
            var previousAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Contains("W3CustomHeroes"));

            AssemblyLoadContext.GetLoadContext(previousAssembly)?.Unload();
        }

        internal class CustomLoadContext : AssemblyLoadContext
        {
            internal CustomLoadContext() : base(isCollectible: true) { }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                //Console.WriteLine($"Loading {assemblyName.Name}");
                if(assemblyName.Name == "WarcraftPlugin")
                {
                    //Ensure the latest version of the assembly is loaded
                    return AppDomain.CurrentDomain.GetAssemblies().Reverse().FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
                }
                return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
            }
        }
    }
}
