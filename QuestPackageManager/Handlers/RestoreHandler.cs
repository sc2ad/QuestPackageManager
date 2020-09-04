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

        public event Action<RestoreHandler, Dictionary<Dependency, SharedConfig>>? OnDependenciesCollected;

        public event Action<RestoreHandler, Dictionary<Dependency, SharedConfig>, Dictionary<string, (Dictionary<string, JsonElement> data, SharedConfig conf)>>? OnRestore;

        public RestoreHandler(IConfigProvider configProvider, IDependencyResolver dependencyResolver)
        {
            this.configProvider = configProvider;
            this.dependencyResolver = dependencyResolver;
        }

        private void CollectDependencies(string thisId, ref Dictionary<Dependency, SharedConfig> myDependencies, Dependency d)
        {
            // Null assertions
            if (d is null)
                throw new ArgumentNullException(nameof(d), Resources.DependencyNull);
            if (d.Id is null)
                throw new ArgumentException(Resources.DependencyIdNull);
            if (d.VersionRange is null)
                throw new ArgumentException($"Dependency: {d.Id} {nameof(d.VersionRange)} is null!");
            if (thisId.Equals(d.Id, StringComparison.OrdinalIgnoreCase))
                throw new DependencyException($"Recursive dependency! Tried to get dependency: {d.Id}, but {thisId} matches {d.Id}!");
            // We want to convert our uri into a config file
            var depConfig = dependencyResolver.GetSharedConfig(d);
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
            // Add our mapping from dependency to config
            myDependencies.Add(d, depConfig);
            // Otherwise, we iterate over all of the config's dependencies
            foreach (var innerD in depConfig.Config.Dependencies)
            {
                if (innerD.Id is null)
                    throw new ConfigException($"Config id: {depConfig.Config.Info.Id} has dependency with null id!");
                if (!myDependencies.TryGetValue(innerD, out var innerDConfig))
                {
                    // Collect dependencies of this inner dependency, which should also add itself to myDependencies.
                    CollectDependencies(thisId, ref myDependencies, innerD);
                }
                // Otherwise, the inner dependency already exists and has a config.
                // We can actually take it easy here, we only need to COLLECT our dependencies, we don't need to COLLAPSE them.
            }
            // When we are done, myDependencies should contain a mapping of ALL of our dependencies (recursively) mapped to their SharedConfigs.
        }

        public Dictionary<Dependency, SharedConfig> CollectDependencies()
        {
            var config = configProvider.GetConfig();
            if (config is null)
                throw new ConfigException(Resources.ConfigNotFound);
            if (config.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            var myDependencies = new Dictionary<Dependency, SharedConfig>();
            foreach (var d in config.Dependencies)
                CollectDependencies(config.Info.Id, ref myDependencies, d);
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
        public static Dictionary<string, (Dictionary<string, JsonElement> data, SharedConfig conf)> CollapseDependencies(Dictionary<Dependency, SharedConfig> deps)
        {
            if (deps is null)
                throw new ArgumentNullException(nameof(deps));
            var uniqueDeps = new Dictionary<string, List<(Dependency dep, SharedConfig conf)>>();
            var collapsed = new Dictionary<string, (Dictionary<string, JsonElement> data, SharedConfig conf)>();
            // For each Dependency, we want to find all other dependencies that have the same ID
            foreach (var dep in deps)
            {
                if (dep.Key.Id is null)
                    continue;
                if (uniqueDeps.TryGetValue(dep.Key.Id.ToUpperInvariant(), out var matchingDeps))
                    matchingDeps.Add((dep.Key, dep.Value));
                else
                    uniqueDeps.Add(dep.Key.Id.ToUpperInvariant(), new List<(Dependency dep, SharedConfig conf)> { (dep.Key, dep.Value) });
            }
            foreach (var p in uniqueDeps)
            {
                if (p.Value.Count == 0)
                    continue;
                var intersection = p.Value[0].dep.VersionRange;
                var confToAdd = p.Value[0].conf;
                // Also collapse the additional data into a single dependency's additional data
                var data = new Dictionary<string, JsonElement>();
                foreach (var d in p.Value[0].dep.AdditionalData)
                    data.Add(d.Key, d.Value);
                for (int i = 1; i < p.Value.Count; i++)
                {
                    // If we have multiple matching dependencies, intersect across all of them.
                    var val = p.Value[i].dep;
                    if (val.VersionRange is null)
                        throw new ConfigException($"Dependency: {val.Id} has a null {nameof(Dependency.VersionRange)}!");
                    var tmp = val.VersionRange.Intersect(intersection);
                    // Now take the intersection, if it is 0.0.0, say "uhoh"
                    if (tmp.ToString() == "<0.0.0")
                        throw new DependencyException($"Dependency: {val.Id} needs version range: {val.VersionRange} which does not intersect: {intersection}");
                    // Now we need to check to see if the current config is of a greater version than the config we want to add
                    // If it is, set it
                    // We can assume SharedConfig has no null fields from CollectDependencies
                    if (p.Value[i].conf.Config?.Info?.Version > confToAdd.Config?.Info?.Version)
                        confToAdd = p.Value[i].conf;
                    intersection = tmp;
                }
                // Add uppercase ID to collapsed mapping
                collapsed.Add(p.Key, (data, confToAdd));
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

            // After all dependencies are grabbed, filter for ones we haven't yet met
            // Collapse our dependencies into unique IDs
            // This can throw, based off of invalid matches
            var collapsed = CollapseDependencies(myDependencies);
            var unrestored = myDependencies.Where(kvp =>
            {
                if (kvp.Key is null
                    || kvp.Key.Id is null
                    || kvp.Key.VersionRange is null)
                    return true;
                var pair = sharedConfig.RestoredDependencies.Find(p => p.id == kvp.Key.Id.ToUpperInvariant());
                if (pair != default((string, SemVer.Version)))
                    return !kvp.Key.VersionRange.IsSatisfied(pair.version);
                return true;
            });

            foreach (var kvp in unrestored)
            {
                // For each of the (non-unique) dependencies, resolve each one.
                // However, we only want to HEADER resolve the unique dependencies
                dependencyResolver.ResolveDependency(config, kvp.Key);
                var key = kvp.Key!.Id!.ToUpperInvariant();
                dependencyResolver.ResolveUniqueDependency(config, collapsed[key]);
                sharedConfig.RestoredDependencies.Add((key, collapsed[key]!.conf.Config!.Info!.Version));
            }
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