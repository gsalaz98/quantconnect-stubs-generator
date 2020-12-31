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
    public class PythonGenerator
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(PythonGenerator));

        private readonly string _leanPath;
        private readonly string _runtimePath;
        private readonly string _outputDirectory;

        public PythonGenerator(string leanPath, string runtimePath, string outputDirectory)
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
            var context = new ParseContext();

            // Parse all syntax trees using all parsers
            ParseSyntaxTrees<ClassParser, PythonType>(context, syntaxTrees, compilation);
            ParseSyntaxTrees<PropertyParser, PythonType>(context, syntaxTrees, compilation);
            ParseSyntaxTrees<MethodParser, PythonType>(context, syntaxTrees, compilation);

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

            // Create empty namespaces to fill gaps in between namespaces like "A.B" and "A.B.C.D"
            // This is needed to make import resolution work correctly
            CreateEmptyNamespaces(context);

            // Render .pyi files containing stubs for all parsed namespaces
            foreach (var ns in context.GetNamespaces())
            {
                var namespacePath = ns.Name.Replace('.', '/');
                var basePath = Path.GetFullPath($"{namespacePath}/__init__", _outputDirectory);

                RenderNamespace(ns, basePath + ".pyi");
                GeneratePyLoader(ns.Name, basePath + ".py");
            }

            // Generate stubs for the clr module
            GenerateClrStubs();

            // Create setup.py
            GenerateSetup();
        }

        private void ParseSyntaxTrees<T, P>(
            ParseContext context,
            IEnumerable<SyntaxTree> syntaxTrees,
            CSharpCompilation compilation)
            where T : BaseParser<P>
            where P : ILanguageType<P>
        {
            foreach (var tree in syntaxTrees)
            {
                Logger.Debug($"Running {typeof(T).Name} on {tree.FilePath}");

                var model = compilation.GetSemanticModel(tree);
                var parser = (T) Activator.CreateInstance(typeof(T), context, model);

                if (parser == null)
                {
                    throw new SystemException($"Could not create {typeof(T).Name} for {tree.FilePath}");
                }

                parser.Visit(tree.GetRoot());
            }
        }

        private void PostProcessClass(Class cls)
        {
            var pythonMethods = new Dictionary<string, PythonType>();
            var pythonMethodsToRemove = new List<Method>();

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

        private void MarkOverloads(Class cls)
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

        private void CreateEmptyNamespaces(ParseContext context)
        {
            // The key is the namespace, the value is whether there is already a namespace for it
            // After adding all namespaces, the keys of entries with a false value represent the gap namespaces
            var namespaceMapping = new Dictionary<string, bool>();

            foreach (var ns in context.GetNamespaces())
            {
                namespaceMapping[ns.Name] = true;

                var parts = ns.Name.Split(".");

                for (var i = 1; i <= parts.Length; i++)
                {
                    var partialNamespace = string.Join(".", parts.Take(i));

                    if (!namespaceMapping.ContainsKey(partialNamespace))
                    {
                        namespaceMapping[partialNamespace] = false;
                    }
                }
            }

            foreach (var (ns, exists) in namespaceMapping)
            {
                if (!exists)
                {
                    context.RegisterNamespace(new Namespace(ns));
                }
            }
        }

        private void RenderNamespace(Namespace ns, string outputPath)
        {
            // Don't generate empty .pyi files
            if (!ns.GetParentClasses().Any())
            {
                return;
            }

            Logger.Info($"Generating {outputPath}");

            EnsureParentDirectoriesExist(outputPath);

            using var writer = new StreamWriter(outputPath);
            var renderer = new NamespaceRenderer(writer, 0);
            renderer.Render(ns);
        }

        private void GeneratePyLoader(string ns, string outputPath)
        {
            Logger.Info($"Generating {outputPath}");

            EnsureParentDirectoriesExist(outputPath);

            using var writer = new StreamWriter(outputPath);
            var renderer = new PyLoaderRenderer(writer);
            renderer.Render(ns);
        }

        private void GenerateClrStubs()
        {
            var outputPath = Path.GetFullPath("clr/__init__.pyi", _outputDirectory);
            Logger.Info($"Generating {outputPath}");

            EnsureParentDirectoriesExist(outputPath);

            using var writer = new StreamWriter(outputPath);
            var renderer = new ClrStubsRenderer(writer);
            renderer.Render();
        }

        private void GenerateSetup()
        {
            var setupPath = Path.GetFullPath("setup.py", _outputDirectory);
            Logger.Info($"Generating {setupPath}");

            EnsureParentDirectoriesExist(setupPath);

            using var writer = new StreamWriter(setupPath);
            var renderer = new SetupRenderer(writer, _leanPath, _outputDirectory);
            renderer.Render();
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
