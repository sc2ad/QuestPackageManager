using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPackageManager
{
    /// <summary>
    /// Restores and resolves dependencies for the given config
    /// </summary>
    public class RestoreHandler
    {
        private readonly IConfigProvider configProvider;
        private readonly IUriHandler uriHandler;

        public event Action<RestoreHandler, List<Dependency>>? OnDependenciesCollected;

        public event Action<RestoreHandler, List<Dependency>>? OnRestore;

        public RestoreHandler(IConfigProvider configProvider, IUriHandler uriHandler)
        {
            this.configProvider = configProvider;
            this.uriHandler = uriHandler;
        }

        private void CollectDependencies(ref List<Dependency> myDependencies, Dependency d)
        {
            // We want to convert our uri into a config file
            var depConfig = uriHandler.GetConfig(d);
            if (depConfig is null)
                throw new ConfigException($"Could not find config for: {d.Id} at: {d.Url}");
            // Then we want to check to ensure that the config file we have gotten is within our version
            if (depConfig.Info is null)
                throw new ConfigException($"Config is of an invalid format for: {d.Id} at: {d.Url} - No info!");
            if (depConfig.Info.Version is null)
                throw new ConfigException($"Config is of an invalid format for: {d.Id} at: {d.Url} - No version!");
            // If it isn't, we fail to match our dependencies, exit out.
            if (!d.VersionRange.IsSatisfied(depConfig.Info.Version))
                throw new DependencyException($"Dependency unmet! Want: {d.VersionRange} got: {depConfig.Info.Version} for: {d.Id} at: {d.Url}");
            // Otherwise, we iterate over all of the config's dependencies
            foreach (var innerD in depConfig.Dependencies)
            {
                var existing = myDependencies.FirstOrDefault(dep => innerD.Id.Equals(dep.Id, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    // For each one, we see if it is a unique ID (one we don't have without our own dependencies) add it if it is
                    myDependencies.Add(innerD);
                    // Collect dependencies of this
                    CollectDependencies(ref myDependencies, innerD);
                }
                else
                {
                    // If it is not, we check to see if our dependency includes the dependency used
                    // ex: MyDep@^0.1.0
                    // TheirDep.Dependencies[MyDep@^0.0.1] should be valid
                    var intersection = existing.VersionRange.Intersect(innerD.VersionRange);
                    if (intersection.ToString() != "<0.0.0")
                        // Case where intersections do not overlap
                        throw new DependencyException($"Dependency range fault! Want: {existing.VersionRange} but dependency: {innerD.Id} (under dependency: {d.Id}) wants: {innerD.VersionRange}");
                    // Otherwise, modify the existing element's version range to match.
                    // This is done with copies, so we don't need to worry about breaking anything.
                    existing.VersionRange = intersection;
                }
                // If the dependencies do not intersect, we can't create two unique, same ID dependencies. Tell user they have unmet dependencies.
            }
        }

        public void CollectDependencies()
        {
            var config = configProvider.GetConfig();
            if (config is null)
                throw new ConfigException(Resources.ConfigNotFound);
            var myDependencies = config.Dependencies.ToList();
            foreach (var d in config.Dependencies)
                CollectDependencies(ref myDependencies, d);
            config.Dependencies.Clear();
            // Call post dependency resolution code
            OnDependenciesCollected?.Invoke(this, myDependencies);
            config.Dependencies.AddRange(myDependencies);
            configProvider.Commit();
        }

        public void Restore()
        {
            var config = configProvider.GetConfig();
            if (config is null)
                throw new ConfigException(Resources.ConfigNotFound);
            var myDependencies = config.Dependencies.ToList();
            foreach (var d in config.Dependencies)
                // For each dependency, collect its dependencies
                CollectDependencies(ref myDependencies, d);

            // After all dependencies are grabbed, compare it with our current met dependencies
            foreach (var d in myDependencies)
            {
                var included = config.IncludedDependencies.FirstOrDefault(dep => d.Id.Equals(dep.Id, StringComparison.OrdinalIgnoreCase));
                if (included is null)
                {
                    // Add d to config.MetDependencies
                    // This involves performing a full download, we call IUriHandler to perform this operation
                    // If it throws, it is the caller's responsibilty to catch
                    // This function is responsible for doing anything necessary to the project in order to ensure the dependency has been resolved correctly.
                    // It should throw if anything fails
                    uriHandler.ResolveDependency(d);
                    config.IncludedDependencies.Add(d);
                }
                else if (included.VersionRange.Intersect(d.VersionRange).ToString() == "<0.0.0")
                    // Case where versions do not intersect
                    // TODO: Add a way to fixup included dependencies
                    throw new DependencyException($"Currently has {included.Id} with version range: {included.VersionRange} which does not match required: {d.VersionRange}");
                // Otherwise, we don't need to do anything. We have already included it.
            }
            configProvider.Commit();
            // Perform additional modification here
            OnRestore?.Invoke(this, myDependencies);

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