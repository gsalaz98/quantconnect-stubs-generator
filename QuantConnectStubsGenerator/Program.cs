using System;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace QuantConnectStubsGenerator
{
    internal static class Program
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

        private static void Main(string[] args)
        {
            XmlConfigurator.Configure(
                LogManager.GetRepository(Assembly.GetEntryAssembly()),
                new FileInfo("log4net.config"));

            if (args.Length != 3)
            {
                Logger.Error("Usage: dotnet run <Lean directory> <runtime directory> <output directory> <language>=python,cpp [optional]");
                Environment.Exit(1);
            }

            if (Environment.GetEnvironmentVariables().Contains("NO_DEBUG"))
            {
                ((Hierarchy) LogManager.GetRepository()).Root.Level = Level.Info;
                ((Hierarchy) LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);
            }

            try
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException($"Expected 3+ arguments, found {args.Length}");
                }

                if (args.Length == 4 && args[3].ToLowerInvariant() == "cpp")
                {
                    new CppGenerator(args[0], args[1], args[2]).Run();
                    return;
                }

                new PythonGenerator(args[0], args[1], args[2]).Run();
            }
            catch (Exception e)
            {
                Logger.Error("Generator crashed", e);
                Environment.Exit(1);
            }
        }
    }
}
