using McMaster.Extensions.CommandLineUtils;
using QPM.Data;
using QPM.Providers;
using QuestPackageManager;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace QPM
{
    [Command("qpm", Description = "Quest package manager")]
    [Subcommand(typeof(PackageCommand), typeof(DependencyCommand), typeof(RestoreCommand), typeof(PublishCommand))]
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
        private static CppPropertiesProvider propertiesProvider;
        private static BmbfModProvider bmbfmodProvider;
        private static AndroidMkProvider androidMkProvider;
        private static QPMApi api;

        public static int Main(string[] args)
        {
            // Create config provider
            configProvider = new LocalConfigProvider(Environment.CurrentDirectory, PackageFileName, LocalFileName);
            api = new QPMApi(configProvider);
            resolver = new RemoteQPMDependencyResolver(api);
            propertiesProvider = new CppPropertiesProvider(Path.Combine(Environment.CurrentDirectory, ".vscode", "c_cpp_properties.json"));
            bmbfmodProvider = new BmbfModProvider(Path.Combine(Environment.CurrentDirectory, "bmbfmod.json"));
            androidMkProvider = new AndroidMkProvider(Path.Combine(Environment.CurrentDirectory, "Android.mk"));
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
                return CommandLineApplication.Execute<Program>(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Utils.WriteFail();
            }
            return -1;
        }

        private static void Program_OnDependencyResolved(Config myConfig, Config config)
        {
            // Handle obtaining .so file from external config
            // Grab the .so file link from AdditionalData and handle it
            if (!config.AdditionalData.TryGetValue("soLink", out var soLink))
                throw new DependencyException("Dependency: {config.Info.Id} has no 'soLink' property! Cannot download so to link!");

            WebClient client = new WebClient();
            var fileLoc = Path.Combine(myConfig.DependenciesDir, (config.Info.Id + "_" + config.Info.Version.ToString()).Replace('.', '_') + ".so");
            if (File.Exists(fileLoc))
                File.Delete(fileLoc);
            client.DownloadFile(soLink, fileLoc);
            // Perform modifications to the Android.mk and c_cpp_properties.json as necessary (I don't think c_cpp_properties.json should change, includePath is constant)
            // But Android.mk needs some things changed:
            // It needs a new module
            // It needs to set some stuff on that new module
            // It needs to use that new module in main build
            var mk = androidMkProvider.GetFile();
            if (mk != null)
            {
                var module = new Module
                {
                    PrefixLines = new List<string>
                    {
                        $"# Creating prebuilt for dependency: {config.Info.Id} - version: {config.Info.Version}",
                        "include $(CLEAR_VARS)"
                    },
                    Id = config.Info.Id,
                    Src = new List<string>
                    {
                        "./" + fileLoc
                    },
                    ExportIncludes = Path.Combine(myConfig.DependenciesDir, config.Info.Id),
                    BuildLine = "include $(PREBUILT_SHARED_LIBRARY)"
                };
                var main = mk.Modules.LastOrDefault();
                if (main != null)
                {
                    if (main.SharedLibs.FirstOrDefault(s => module.Id.Equals(s, StringComparison.OrdinalIgnoreCase)) is null)
                        main.SharedLibs.Add(module.Id);
                }
                var existing = mk.Modules.FindIndex(m => module.Id.Equals(m.Id, StringComparison.OrdinalIgnoreCase));
                if (existing == -1)
                    mk.Modules.Insert(mk.Modules.Count - 1, module);
                else
                {
                    mk.Modules[existing].Src = module.Src;
                    mk.Modules[existing].ExportIncludes = module.ExportIncludes;
                }
                androidMkProvider.SerializeFile(mk);
            }
        }

        private static void DependencyHandler_OnDependencyRemoved(DependencyHandler handler, Dependency dependency)
        {
            // Handle deletion of the dependency in question
            // That would include removing it from the config.SharedDir, removing it from Android.mk, removing it from bmbfmod.json
            var mk = androidMkProvider.GetFile();
            if (mk != null)
            {
                // Remove module
                mk.Modules.RemoveAll(m => m.Id.Equals(dependency.Id, StringComparison.OrdinalIgnoreCase));
                // Main module, remove shared library
                var module = mk.Modules.LastOrDefault();
                if (module != null)
                {
                    module.RemoveSharedLibrary(dependency.Id);
                }
            }
            // TODO: Remove from bmbfmod.json
            var cfg = configProvider.GetConfig();
            var localConfig = configProvider.GetLocalConfig();
            // If we have it in our met dependencies
            if (cfg != null && localConfig != null && localConfig.IncludedDependencies.Find(d => dependency.Id.Equals(d.Id, StringComparison.OrdinalIgnoreCase)) != null)
                resolver.RemoveDependency(cfg, dependency);
        }

        private static void PackageHandler_OnNameChanged(PackageHandler handler, string name)
        {
            // Perform bmbfmod.json edits to name
            var mod = bmbfmodProvider.GetMod();
            if (mod != null)
            {
                mod.UpdateName(name);
                bmbfmodProvider.SerializeMod(mod);
            }
        }

        private static void PackageHandler_OnVersionChanged(PackageHandler handler, SemVer.Version version)
        {
            // Perform Android.mk, c_cpp_properties.json, bmbfmod.json edits to version
            var props = propertiesProvider.GetProperties();
            if (props != null)
            {
                props.UpdateVersion(version);
                propertiesProvider.SerializeProperties(props);
            }
            var mod = bmbfmodProvider.GetMod();
            if (mod != null)
            {
                mod.UpdateVersion(version);
                bmbfmodProvider.SerializeMod(mod);
            }
            var mk = androidMkProvider.GetFile();
            if (mk != null)
            {
                var module = mk.Modules.LastOrDefault();
                if (module != null)
                {
                    module.AddDefine("VERSION", version.ToString());
                    androidMkProvider.SerializeFile(mk);
                }
            }
        }

        private static void PackageHandler_OnPackageCreated(PackageHandler handler, PackageInfo info)
        {
            // Perform Android.mk, c_cpp_properties.json, bmbfmod.json edits to ID, version, name, other info (?)
            var cfg = configProvider.GetConfig();
            string shared = null;
            string depDir = null;
            if (cfg != null)
            {
                shared = "./" + cfg.SharedDir;
                depDir = "./" + cfg.DependenciesDir;
                var actualShared = Path.Combine(Environment.CurrentDirectory, cfg.SharedDir);
                var actualDeps = Path.Combine(Environment.CurrentDirectory, cfg.DependenciesDir);
                if (!Directory.Exists(actualShared))
                    Directory.CreateDirectory(actualShared);
                if (!Directory.Exists(actualDeps))
                    Directory.CreateDirectory(actualDeps);
            }
            var props = propertiesProvider.GetProperties();
            if (props != null)
            {
                props.UpdateVersion(info.Version);
                props.UpdateId(info.Id);
                if (cfg != null)
                {
                    props.AddIncludePath(shared);
                    props.AddIncludePath(depDir);
                }
                propertiesProvider.SerializeProperties(props);
            }
            var mod = bmbfmodProvider.GetMod();
            if (mod != null)
            {
                mod.UpdateVersion(info.Version);
                mod.UpdateId(info.Id);
                bmbfmodProvider.SerializeMod(mod);
            }
            var mk = androidMkProvider.GetFile();
            if (mk != null)
            {
                var module = mk.Modules.LastOrDefault();
                if (module != null)
                {
                    module.AddDefine("ID", info.Id);
                    module.AddDefine("VERSION", info.Version.ToString());
                    // Also add includePath for myConfig
                    if (cfg != null)
                    {
                        module.AddIncludePath(shared);
                        module.AddIncludePath(depDir);
                    }
                    androidMkProvider.SerializeFile(mk);
                }
            }
        }

        private static void PackageHandler_OnIdChanged(PackageHandler handler, string id)
        {
            // Perform Android.mk, c_cpp_properties.json, bmbfmod.json edits to ID
            var props = propertiesProvider.GetProperties();
            if (props != null)
            {
                props.UpdateId(id);
                propertiesProvider.SerializeProperties(props);
            }
            var mod = bmbfmodProvider.GetMod();
            if (mod != null)
            {
                mod.UpdateId(id);
                bmbfmodProvider.SerializeMod(mod);
            }
            var mk = androidMkProvider.GetFile();
            if (mk != null)
            {
                var module = mk.Modules.LastOrDefault();
                if (module != null)
                {
                    module.AddDefine("ID", id);
                    androidMkProvider.SerializeFile(mk);
                }
            }
        }

        private void OnExecute(CommandLineApplication app) => app.ShowHelp();
    }
}