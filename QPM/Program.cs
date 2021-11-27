using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using QPM.Commands;
using QPM.Data;
using QPM.Providers;
using QuestPackageManager;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;

namespace QPM
{
    [Command("qpm", Description = "Quest package manager")]
    [Subcommand(typeof(PackageCommand),
        typeof(DependencyCommand),
        typeof(RestoreCommand),
        typeof(CollectCommand),
        typeof(CollapseCommand),
        typeof(PublishCommand),
        typeof(SupportedPropertiesCommand),
        typeof(CacheCommand),
        typeof(ClearCommand),
        typeof(VersionCommand),
        typeof(ConfigCommand)
    )]
    internal class Program
    {
        internal const string PackageFileName = "qpm.json";
        internal const string LocalFileName = "qpm.shared.json";
        internal static DependencyHandler DependencyHandler { get; private set; }
        internal static PackageHandler PackageHandler { get; private set; }
        internal static RestoreHandler RestoreHandler { get; private set; }
        internal static PublishHandler PublishHandler { get; private set; }
        internal static QPMConfig Config { get; private set; } = new QPMConfig();

        internal static IConfigProvider configProvider;
        private static IDependencyResolver resolver;
        private static CppPropertiesProvider propertiesProvider;
        private static BmbfModProvider bmbfmodProvider;
        private static AndroidMkProvider androidMkProvider;
        private static QPMApi api;

        private static readonly string configPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "appsettings.json");

        // TODO: Local config should be DIFFERENT than global config.
        // Both should also be accessible, as in, they should not share the same instance.
        private const string qpmLocalConfig = "qpm.config.json";

        internal static bool isLocal = false;
        private static readonly JsonSerializerOptions options = new() { WriteIndented = true };

        private static void LoadConfig()
        {
            try
            {
                if (File.Exists(qpmLocalConfig))
                {
                    // Load local config if it exists
                    Console.WriteLine($"Found local config at: {qpmLocalConfig}");
                    var tmp = JsonSerializer.Deserialize<QPMConfig>(File.ReadAllText(qpmLocalConfig));
                    if (tmp is not null)
                        Config = tmp;
                    isLocal = true;
                    return;
                }
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"Creating config at: {configPath}");
                    // Write default settings always
                    File.WriteAllText(configPath, JsonSerializer.Serialize(Config, options));
                }
                else
                {
                    Console.WriteLine($"Found config at: {configPath}");
                    var tmp = JsonSerializer.Deserialize<QPMConfig>(File.ReadAllText(configPath));
                    if (tmp is not null)
                        Config = tmp;
                }
            }
            catch
            {
                // If we can't access the config for any reason, whatever dude.
                Console.WriteLine($"Configuration could not be loaded!");
            }
        }

        internal static async Task SaveConfig()
        {
            if (!isLocal)
            {
                await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(Config, options)).ConfigureAwait(false);
            }
            else
            {
                await File.WriteAllTextAsync(qpmLocalConfig, JsonSerializer.Serialize(Config, options)).ConfigureAwait(false);
            }
        }

        public static int Main(string[] args)
        {
            LoadConfig();
            // Create config provider
            configProvider = new LocalConfigProvider(Environment.CurrentDirectory, PackageFileName, LocalFileName);
            api = new QPMApi(configProvider, Config.DependencyTimeoutSeconds);
            androidMkProvider = new AndroidMkProvider(Path.Combine(Environment.CurrentDirectory, "Android.mk"));
            resolver = new RemoteQPMDependencyResolver(api, androidMkProvider);
            propertiesProvider = new CppPropertiesProvider(Path.Combine(Environment.CurrentDirectory, ".vscode", "c_cpp_properties.json"));
            bmbfmodProvider = new BmbfModProvider(Path.Combine(Environment.CurrentDirectory, "bmbfmod.json"));
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
            // TODO: AKLJSHFJKGHDKJ
            RestoreHandler.OnRestore += (resolver as RemoteQPMDependencyResolver)!.OnRestore;

            // Create configuration/load it if it exists

            try
            {
                return CommandLineApplication.Execute<Program>(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine();
                Console.WriteLine(e);
                Utils.WriteFail();
            }
            return -1;
        }

        private static void DependencyHandler_OnDependencyRemoved(DependencyHandler handler, Dependency dependency)
        {
            // Handle deletion of the dependency in question
            // That would include removing it from the config.SharedDir, removing it from Android.mk, removing it from bmbfmod.json
            if (dependency.Id is null)
                throw new DependencyException("Cannot remove a dependency that does not have a valid Id!");
            var mk = androidMkProvider.GetFile();
            if (mk != null)
            {
                // Remove module, don't remove null ids, though they shouldn't exist.
                mk.Modules.RemoveAll(m => m.Id?.Equals(dependency.Id, StringComparison.OrdinalIgnoreCase) ?? false);
                // Main module, remove shared library
                var module = mk.Modules.LastOrDefault();
                if (module != null)
                {
                    module.RemoveLibrary(dependency.Id);
                }
            }
            // TODO: Remove from bmbfmod.json
            var cfg = configProvider.GetConfig();
            // If we have it in our met dependencies
            if (cfg != null)
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
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException("Config is null!");
            if (conf.Info is null)
                throw new ConfigException("Config info is null!");
            if (conf.Info.Id is null)
                throw new ConfigException("Config ID is null!");
            bool overrodeName = conf.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.OverrideSoName, out var overridenName);
            var mk = androidMkProvider.GetFile();
            if (mk != null)
            {
                var module = mk.Modules.LastOrDefault();
                if (module != null)
                {
                    module.AddDefine("VERSION", version.ToString());
                    if (overrodeName)
                        module.Id = overridenName.GetString()!.ReplaceFirst("lib", "").ReplaceLast(".so", "").ReplaceLast(".a", "");
                    else
                        module.EnsureIdIs(conf.Info.Id, version);
                    androidMkProvider.SerializeFile(mk);
                }
            }
        }

        private static void PackageHandler_OnPackageCreated(PackageHandler handler, PackageInfo info)
        {
            // Perform Android.mk, c_cpp_properties.json, bmbfmod.json edits to ID, version, name, other info (?)
            var cfg = configProvider.GetConfig();
            string depDir;
            string shared;
            if (cfg != null)
            {
                shared = cfg.SharedDir;
                depDir = cfg.DependenciesDir;
                var actualShared = Path.Combine(Environment.CurrentDirectory, cfg.SharedDir);
                var actualDeps = Path.Combine(Environment.CurrentDirectory, cfg.DependenciesDir);
                if (!Directory.Exists(actualShared))
                    Directory.CreateDirectory(actualShared);
                if (!Directory.Exists(actualDeps))
                    Directory.CreateDirectory(actualDeps);
            }
            else
            {
                throw new InvalidOperationException("Config that has just been created cannot possibly be null!");
            }
            var props = propertiesProvider.GetProperties();
            if (props != null)
            {
                props.UpdateVersion(info.Version);
                props.UpdateId(info.Id);
                if (cfg != null)
                {
                    props.AddIncludePath("${workspaceFolder}/" + shared);
                    props.AddIncludePath("${workspaceFolder}/" + depDir);
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
                        if (cfg.Info!.AdditionalData.TryGetValue(SupportedPropertiesCommand.OverrideSoName, out var overridenName))
                        {
                            module.Id = overridenName.GetString()!.ReplaceFirst("lib", "").ReplaceLast(".so", "").ReplaceLast(".a", "");
                        }
                        else
                            module.EnsureIdIs(info.Id, info.Version);
                        module.AddIncludePath("./" + shared);
                        module.AddIncludePath("./" + depDir);
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
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException("Config is null!");
            if (conf.Info is null)
                throw new ConfigException("Config info is null!");
            if (conf.Info.Version is null)
                throw new ConfigException("Config ID is null!");
            var mk = androidMkProvider.GetFile();
            if (mk != null)
            {
                var module = mk.Modules.LastOrDefault();
                if (module != null)
                {
                    module.AddDefine("ID", id);
                    if (conf.Info.AdditionalData.TryGetValue(SupportedPropertiesCommand.OverrideSoName, out var overridenName))
                        module.Id = overridenName.GetString()!.Replace("lib", "").Replace(".so", "").ReplaceLast(".a", "");
                    else
                        module.EnsureIdIs(id, conf.Info.Version);
                    androidMkProvider.SerializeFile(mk);
                }
            }
        }

        private void OnExecute(CommandLineApplication app) => app.ShowHelp();
    }
}