using McMaster.Extensions.CommandLineUtils;
using QuestPackageManager;
using QuestPackageManager.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QPM
{
    [Command("qpm", Description = "Quest package manager")]
    [Subcommand(typeof(PackageCommand), typeof(DependencyCommand), typeof(RestoreCommand)/*, typeof(PublishCommand) */)]
    internal class Program
    {
        internal const string PackageFileName = "qpm.json";
        internal const string LocalFileName = "qpm.lock.json";
        internal static DependencyHandler DependencyHandler { get; private set; }
        internal static PackageHandler PackageHandler { get; private set; }
        internal static RestoreHandler RestoreHandler { get; private set; }
        internal static PublishHandler PublishHandler { get; private set; }

        private static IConfigProvider configProvider;
        private static IDependencyResolver resolver;
        private static QPMApi api;

        public static int Main(string[] args)
        {
            // Create config provider
            configProvider = new LocalConfigProvider(Environment.CurrentDirectory, PackageFileName, LocalFileName);
            api = new QPMApi(configProvider);
            resolver = new RemoteQPMDependencyResolver(api);
            // Create handlers
            PackageHandler = new PackageHandler(configProvider);
            DependencyHandler = new DependencyHandler(configProvider);
            RestoreHandler = new RestoreHandler(configProvider, resolver);
            PublishHandler = new PublishHandler(configProvider, api);
            // Register callbacks
            PackageHandler.OnPackageCreated += PackageHandler_OnPackageCreated;
            PackageHandler.OnIdChanged += PackageHandler_OnIdChanged;
            PackageHandler.OnVersionChanged += PackageHandler_OnVersionChanged;
            PackageHandler.OnNameChanged += PackageHandler_OnNameChanged;
            DependencyHandler.OnDependencyRemoved += DependencyHandler_OnDependencyRemoved;
            (resolver as RemoteQPMDependencyResolver).OnDependencyResolved += Program_OnDependencyResolved;

            try
            {
                int exit = CommandLineApplication.Execute<Program>(args);
                if (exit == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Success!");
                    Console.ResetColor();
                }
                return exit;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed!");
                Console.ResetColor();
            }
            return -1;
        }

        private static void Program_OnDependencyResolved(Config myConfig, Config config)
        {
            // Handle obtaining .so file from external config
            // Grab the .so file link from AdditionalData and handle it
            // Perform modifications to the Android.mk and c_cpp_properties.json as necessary (I don't think c_cpp_properties.json should change, includePath is constant)
            // But Android.mk needs some things changed:
            // It needs a new module
            // It needs to set some stuff on that new module
            // It needs to use that new module in main build
        }

        private static void DependencyHandler_OnDependencyRemoved(DependencyHandler handler, Dependency dependency)
        {
            // Handle deletion of the dependency in question
            // That would include removing it from the config.SharedDir, removing it from Android.mk, removing it from bmbfmod.json
        }

        private static void PackageHandler_OnNameChanged(PackageHandler handler, string name)
        {
            // Perform Android.mk, c_cpp_properties.json, bmbfmod.json edits to name
        }

        private static void PackageHandler_OnVersionChanged(PackageHandler handler, SemVer.Version version)
        {
            // Perform Android.mk, c_cpp_properties.json, bmbfmod.json edits to version
        }

        private static void PackageHandler_OnPackageCreated(PackageHandler handler, PackageInfo info)
        {
            // Perform Android.mk, c_cpp_properties.json, bmbfmod.json edits to ID, version, name, other info (?)
        }

        private static void PackageHandler_OnIdChanged(PackageHandler handler, string id)
        {
            // Perform Android.mk, c_cpp_properties.json, bmbfmod.json edits to ID
        }

        private void OnExecute(CommandLineApplication app) => app.ShowHelp();
    }
}