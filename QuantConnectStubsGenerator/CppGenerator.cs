using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuantConnectStubsGenerator.Model;
using QuantConnectStubsGenerator.Parser;
using QuantConnectStubsGenerator.Renderer;

namespace QuantConnectStubsGenerator
{
    public class CppGenerator
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(PythonGenerator));

        private readonly string _leanPath;
        private readonly string _runtimePath;
        private readonly string _outputDirectory;

        public CppGenerator(string leanPath, string runtimePath, string outputDirectory)
        {
            _leanPath = FormatPath(leanPath);
            _runtimePath = FormatPath(runtimePath);
            _outputDirectory = FormatPath(outputDirectory);
        }

        public void Run()
        {
            // Lean projects not to generate stubs for
            var blacklistedProjects = new[]
            {
                // Example projects
                "Algorithm.CSharp",
                "Algorithm.FSharp",
                "Algorithm.Python",
                "Algorithm.VisualBasic",

                // Other non-relevant projects
                "PythonToolbox",
                "Tests",
                "ToolBox"
            };

            // Path prefixes for all blacklisted projects
            var blacklistedPrefixes = blacklistedProjects
                .Select(project => $"{_leanPath}/{project}")
                .ToList();

            // Find all C# files in non-blacklisted projects in Lean
            var sourceFiles = Directory
                .EnumerateFiles(_leanPath, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains("/bin/"))
                .Where(file => !blacklistedPrefixes.Any(file.StartsWith))
                .ToList();

            // Find all relevant C# files in the C# runtime
            foreach (var (relativePath, searchPattern) in new Dictionary<string, string>
            {
                {"src/libraries/System.Private.CoreLib/src", "*.cs"},
                {"src/mono/netcore/System.Private.CoreLib/src", "*.Mono.cs"},
                {"src/libraries/System.Drawing.Primitives/src", "*.cs"},
                {"src/libraries/System.Collections.Immutable/src", "*.cs"}
            })
            {
                var absolutePath = Path.GetFullPath(relativePath, _runtimePath);
                var files = Directory
                    .EnumerateFiles(absolutePath, searchPattern, SearchOption.AllDirectories)
                    .ToList();

                sourceFiles.AddRange(files);
            }

            Logger.Info($"Parsing {sourceFiles.Count} C# files");

            // Create syntax trees for all C# files
            var syntaxTrees = sourceFiles
                .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
                .ToList();

            // Create a compilation containing all syntax trees to retrieve semantic models from
            var compilation = CSharpCompilation.Create("").AddSyntaxTrees(syntaxTrees);

            // Add all assemblies in current project to compilation to improve semantic models
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && assembly.Location != "")
                {
                    compilation = compilation.AddReferences(MetadataReference.CreateFromFile(assembly.Location));
                }
            }

            // Create an empty ParseContext which will be filled with all relevant information during parsing
            var context = new ParseContext<CppType>();

            // Parse all syntax trees using all parsers
            ParseSyntaxTrees<CppType, CppClassParser>(context, syntaxTrees, compilation);
            ParseSyntaxTrees<CppType, CppPropertyParser>(context, syntaxTrees, compilation);
            ParseSyntaxTrees<CppType, CppMethodParser>(context, syntaxTrees, compilation);

            // Perform post-processing on all parsed classes
            foreach (var ns in context.GetNamespaces())
            {
                foreach (var cls in ns.GetClasses())
                {
                    // Remove Python implementations for methods where there is both a Python as well as a C# implementation
                    // The parsed C# implementation is usually more useful for autocompletion
                    // To improve it a little bit we move the return type of the Python implementation to the C# implementation
                    PostProcessClass(cls);

                    // Mark methods which appear multiple times as overloaded
                    MarkOverloads(cls);
                }
            }

            // Render .pyi files containing stubs for all parsed namespaces
            foreach (var ns in context.GetNamespaces())
            {
                var namespacePath = ns.Name.Replace('.', '/');
                var basePath = Path.GetFullPath($"{namespacePath}", _outputDirectory);

                RenderNamespace(ns, basePath + ".h");
            }

            // Generate stubs for the clr module
            //GenerateClrStubs();

            // Create setup.py
            //GenerateSetup();
        }

        private void ParseSyntaxTrees<T, P>(
            ParseContext<T> context,
            IEnumerable<SyntaxTree> syntaxTrees,
            CSharpCompilation compilation)
            where T : ILanguageType<T>, new()
            where P : BaseParser<T>
        {
            foreach (var tree in syntaxTrees)
            {
                Logger.Debug($"Running {typeof(P).Name} on {tree.FilePath}");

                var model = compilation.GetSemanticModel(tree);
                var parser = (P) Activator.CreateInstance(typeof(P), context, model);

                if (parser == null)
                {
                    throw new SystemException($"Could not create {typeof(P).Name} for {tree.FilePath}");
                }

                parser.Visit(tree.GetRoot());
            }
        }

        private void PostProcessClass(Class<CppType> cls)
        {
            var pythonMethods = new Dictionary<string, CppType>();
            var pythonMethodsToRemove = new List<Method<CppType>>();

            foreach (var method in cls.Methods.Where(m => m.File != null && m.File.EndsWith(".Python.cs")))
            {
                pythonMethods[method.Name] = method.ReturnType;
                pythonMethodsToRemove.Add(method);
            }

            foreach (var method in pythonMethodsToRemove)
            {
                cls.Methods.Remove(method);
            }

            foreach (var (methodName, returnType) in pythonMethods)
            {
                foreach (var method in cls.Methods.Where(m => m.Name == methodName))
                {
                    method.ReturnType = returnType;
                }
            }
        }

        private void MarkOverloads(Class<CppType> cls)
        {
            var duplicateMethodNames = cls.Methods
                .GroupBy(m => m.Name)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var name in duplicateMethodNames)
            {
                foreach (var method in cls.Methods.Where(m => m.Name == name))
                {
                    method.Overload = true;
                }
            }
        }

        private void RenderNamespace(Namespace<CppType> ns, string outputPath)
        {
            // Don't generate empty header files
            if (!ns.GetParentClasses().Any())
            {
                return;
            }

            Logger.Info($"Generating {outputPath}");

            EnsureParentDirectoriesExist(outputPath);

            using var writer = new StreamWriter(outputPath);
            var renderer = new CppNamespaceRenderer(writer, 0);
            renderer.Render(ns);
        }

        private void EnsureParentDirectoriesExist(string path)
        {
            new FileInfo(path).Directory?.Create();
        }

        private string FormatPath(string path)
        {
            var cwd = Directory.GetCurrentDirectory();
            var resolvedPath = Path.GetFullPath(path, cwd);

            var normalizedPath = resolvedPath.Replace('\\', '/');

            return normalizedPath.EndsWith("/")
                ? normalizedPath.Substring(0, path.Length - 1)
                : normalizedPath;
        }
    }
}
