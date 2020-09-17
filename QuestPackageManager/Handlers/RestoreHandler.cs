using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuestPackageManager
{
    /// <summary>
    /// Restores and resolves dependencies for the given config
    /// </summary>
    public class RestoreHandler
    {
        private readonly IConfigProvider configProvider;
        private readonly IDependencyResolver dependencyResolver;

        public event Action<RestoreHandler, Dictionary<RestoredDependencyPair, SharedConfig>>? OnDependenciesCollected;

        public event Action<RestoreHandler, Dictionary<RestoredDependencyPair, SharedConfig>, Dictionary<RestoredDependencyPair, SharedConfig>>? OnRestore;

        public RestoreHandler(IConfigProvider configProvider, IDependencyResolver dependencyResolver)
        {
            this.configProvider = configProvider;
            this.dependencyResolver = dependencyResolver;
        }

        private void CollectDependencies(string thisId, ref Dictionary<RestoredDependencyPair, SharedConfig> myDependencies, RestoredDependencyPair pair)
        {
            // pair contains simply a Dependency in most cases, but if it contains a version already, then we map that version to a SharedConfig
            // Null assertions
            if (pair is null || pair.Dependency is null)
                throw new ArgumentNullException(nameof(pair), Resources.DependencyNull);
            var d = pair.Dependency!;
            if (d.Id is null)
                throw new ArgumentException(Resources.DependencyIdNull);
            if (d.VersionRange is null)
                throw new ArgumentException($"Dependency: {d.Id} {nameof(d.VersionRange)} is null!");
            if (thisId.Equals(d.Id, StringComparison.OrdinalIgnoreCase))
                throw new DependencyException($"Recursive dependency! Tried to get dependency: {d.Id}, but {thisId} matches {d.Id}!");
            // We want to convert our uri into a config file
            var depConfig = dependencyResolver.GetSharedConfig(pair);
            if (depConfig is null)
                throw new ConfigException($"Could not find config for: {d.Id}");
            // Then we want to check to ensure that the config file we have gotten is within our version
            if (depConfig.Config is null)
                throw new ConfigException($"Confid is of an invalid format for: {d.Id} - No config!");
            if (depConfig.Config.Info is null)
                throw new ConfigException($"Config is of an invalid format for: {d.Id} - No info!");
            if (string.IsNullOrEmpty(depConfig.Config.Info.Id))
                throw new ConfigException($"Config is of an invalid format for: {d.Id} - No Id!");
            // Check to make sure the config's version matches our dependency's version
            if (!depConfig.Config.Info.Id.Equals(d.Id, StringComparison.OrdinalIgnoreCase))
                throw new ConfigException($"Dependency and config have different ids! {d.Id} != {depConfig.Config.Info.Id}!");
            if (depConfig.Config.Info.Version is null)
                throw new ConfigException($"Config is of an invalid format for: {d.Id} - No Version!");
            // If it isn't, we fail to match our dependencies, exit out.
            if (!d.VersionRange.IsSatisfied(depConfig.Config.Info.Version))
                throw new DependencyException($"Dependency unmet! Want: {d.VersionRange} got: {depConfig.Config.Info.Version} for: {d.Id}");
            if (pair.Version != null && pair.Version != depConfig.Config.Info.Version)
                throw new ConfigException($"Wanted specific version: {pair.Version} but got: {depConfig.Config.Info.Version} for: {d.Id}");
            var toAdd = new RestoredDependencyPair { Dependency = d, Version = depConfig.Config.Info.Version };
            if (!myDependencies.ContainsKey(toAdd))
            {
                // We need to double check here, just to make sure we don't accidentally add when we literally have a potential match:
                if (myDependencies.Keys.FirstOrDefault(item => toAdd.Dependency.Id.Equals(item.Dependency!.Id, StringComparison.OrdinalIgnoreCase) && toAdd.Version == item.Version) is null)
                    // If there is no exactly matching key:
                    // Add our mapping from dependency to config
                    myDependencies.Add(toAdd, depConfig);
            }
            // Otherwise, we iterate over all of the config's RESTORED dependencies
            // That is, all of the dependencies that we used to actually build this
            foreach (var innerD in depConfig.RestoredDependencies)
            {
                if (innerD.Dependency is null || innerD.Version is null)
                    throw new ConfigException($"A restored dependency in config for: {depConfig.Config.Info.Id} version: {depConfig.Config.Info.Version} has a null dependency or version property!");
                // For each of the config's dependencies, collect all of the restored dependencies for it,
                // if we have no RestoredDependencies that match the ID, VersionRange, and Version already (since those would be the same).
                CollectDependencies(thisId, ref myDependencies, innerD);
                // We can actually take it easy here, we only need to COLLECT our dependencies, we don't need to COLLAPSE them.
            }
            // When we are done, myDependencies should contain a mapping of ALL of our dependencies (recursively) mapped to their SharedConfigs.
        }

        public Dictionary<RestoredDependencyPair, SharedConfig> CollectDependencies()
        {
            var config = configProvider.GetConfig();
            if (config is null)
                throw new ConfigException(Resources.ConfigNotFound);
            if (config.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            var myDependencies = new Dictionary<RestoredDependencyPair, SharedConfig>();
            foreach (var d in config.Dependencies)
                CollectDependencies(config.Info.Id, ref myDependencies, new RestoredDependencyPair { Dependency = d });
            // Call post dependency resolution code
            OnDependenciesCollected?.Invoke(this, myDependencies);
            return myDependencies;
        }

        /// <summary>
        /// Collapses a fully saturated mapping of <see cref="Dependency"/> to <see cref="SharedConfig"/>
        /// with one or more <see cref="Dependency"/> objects having the same <see cref="Dependency.Id"/> field.
        /// </summary>
        /// <param name="deps">Mapping to collapse</param>
        /// <returns>A mapping of unique uppercased dependency IDs to <see cref="SharedConfig"/></returns>
        public static Dictionary<RestoredDependencyPair, SharedConfig> CollapseDependencies(Dictionary<RestoredDependencyPair, SharedConfig> deps)
        {
            if (deps is null)
                throw new ArgumentNullException(nameof(deps));
            var uniqueDeps = new Dictionary<string, List<(RestoredDependencyPair dep, SharedConfig conf)>>();
            var collapsed = new Dictionary<RestoredDependencyPair, SharedConfig>();
            // For each Dependency, we want to find all other dependencies that have the same ID
            foreach (var dep in deps)
            {
                if (dep.Key.Dependency is null || dep.Key.Dependency.Id is null)
                    continue;
                var id = dep.Key.Dependency.Id;
                if (uniqueDeps.TryGetValue(id.ToUpperInvariant(), out var matchingDeps))
                    matchingDeps.Add((dep.Key, dep.Value));
                else
                    uniqueDeps.Add(id.ToUpperInvariant(), new List<(RestoredDependencyPair dep, SharedConfig conf)> { (dep.Key, dep.Value) });
            }
            foreach (var p in uniqueDeps)
            {
                if (p.Value.Count == 0)
                    continue;
                var depToAdd = new Dependency(p.Value[0].dep.Dependency!.Id!, p.Value[0].dep.Dependency!.VersionRange!);
                foreach (var kvp in p.Value[0].dep.Dependency!.AdditionalData)
                    depToAdd.AdditionalData.Add(kvp.Key, kvp.Value);
                var confToAdd = p.Value[0].conf;
                // Also collapse the additional data into a single dependency's additional data
                for (int i = 1; i < p.Value.Count; i++)
                {
                    // If we have multiple matching dependencies, intersect across all of them.
                    var val = p.Value[i].dep.Dependency!;
                    if (val.VersionRange is null)
                        throw new ConfigException($"Dependency: {val.Id} has a null {nameof(Dependency.VersionRange)}!");
                    var tmp = val.VersionRange.Intersect(depToAdd.VersionRange);
                    // Now take the intersection, if it is 0.0.0, say "uhoh"
                    if (tmp.ToString() == "<0.0.0")
                        throw new DependencyException($"Dependency: {val.Id} needs version range: {val.VersionRange} which does not intersect: {depToAdd.VersionRange}");
                    // Now we need to check to see if the current config is of a greater version than the config we want to add
                    // If it is, set it
                    // We can assume SharedConfig has no null fields from CollectDependencies
                    if (p.Value[i].conf.Config?.Info?.Version > confToAdd.Config?.Info?.Version)
                        confToAdd = p.Value[i].conf;
                    // Copy additional data
                    foreach (var pair in p.Value[i].dep.Dependency!.AdditionalData)
                        depToAdd.AdditionalData.Add(pair.Key, pair.Value);
                    depToAdd.VersionRange = tmp;
                }
                // Add to collapsed mapping
                collapsed.Add(new RestoredDependencyPair
                {
                    Dependency = depToAdd,
                    Version = confToAdd.Config!.Info!.Version
                }, confToAdd);
            }
            return collapsed;
        }

        /// <summary>
        ///
        /// </summary>
        public void Restore()
        {
            var config = configProvider.GetConfig();
            if (config is null)
                throw new ConfigException(Resources.ConfigNotFound);
            var sharedConfig = configProvider.GetSharedConfig(true);
            if (sharedConfig is null)
                throw new ConfigException(Resources.LocalConfigNotCreated);
            if (config.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            var myDependencies = CollectDependencies();

            // Collapse our dependencies into unique IDs
            // This can throw, based off of invalid matches
            var collapsed = CollapseDependencies(myDependencies);
            // Clear all restored dependencies to prepare
            sharedConfig.RestoredDependencies.Clear();

            foreach (var kvp in myDependencies)
            {
                // For each of the (non-unique) dependencies, resolve each one.
                // However, we only want to HEADER resolve the unique dependencies
                dependencyResolver.ResolveDependency(config, kvp.Key);
                sharedConfig.RestoredDependencies.Add(kvp.Key);
            }
            foreach (var val in collapsed)
                dependencyResolver.ResolveUniqueDependency(config, val);
            // Perform additional modification here
            OnRestore?.Invoke(this, myDependencies, collapsed);
            configProvider.Commit();

            // Collect dependencies and resolve them.
            // This should just involve grabbing them from GH or whatever URL is provided, ensuring versions match
            // Then, we have to add the headers to our include path (so anything that uses headers should be on include path)
            // In addition to modifying Android.mk and c_cpp_properties.json to match this.
            // We also need to ensure that our headers are in a .gitignored folder
            // And that all of the headers are placed into this folder with a global folder as its ID.
            // Therefore, technically only the root folder needs to be in our include path
            // Ideally, the include path modifications can be done separately from this action
        }
    }
}