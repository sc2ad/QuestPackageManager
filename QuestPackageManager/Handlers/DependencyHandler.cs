using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPackageManager
{
    public class DependencyHandler
    {
        private readonly IConfigProvider configProvider;

        // bool represents if dependency already existed
        public event Action<DependencyHandler, Config, Dependency, bool>? OnConfigDependencyAdded;

        // bool represents if dependency already existed
        public event Action<DependencyHandler, Dependency, bool>? OnDependencyAdded;

        public event Action<DependencyHandler, Config, Dependency>? OnConfigDependencyRemoved;

        public event Action<DependencyHandler, Dependency>? OnDependencyRemoved;

        public DependencyHandler(IConfigProvider configProvider)
        {
            this.configProvider = configProvider;
        }

        public void AddDependency(string id, SemVer.Range range) => AddDependency(new Dependency(id, range));

        public void AddDependency(Dependency dep)
        {
            if (dep is null)
                throw new ArgumentNullException(nameof(dep), Resources.DependencyNull);
            if (dep.Id is null)
                throw new ArgumentException(Resources.DependencyIdNull);
            // This should be faily straightforward:
            // The given dependency should be added to the config, the config should be committed
            // Then we should perform (automatically or manually) restore in order to ensure we can obtain this dependency.
            // If we are adding a dependency that already exists (by id) we update the version to match this.
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotFound);
            if (conf.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            if (conf.Info.Id.Equals(dep.Id, StringComparison.OrdinalIgnoreCase))
                throw new DependencyException($"Recursive dependency! Tried to add dependency: {dep.Id}, but package ID matches!");
            // Ids are not case sensitive
            var existing = conf.Dependencies.FirstOrDefault(d => dep.Id.Equals(d.Id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.VersionRange = (dep ?? throw new ArgumentNullException(Resources.Dependency)).VersionRange;
                existing.AdditionalData.Clear();
                foreach (var p in dep.AdditionalData)
                    existing.AdditionalData.Add(p.Key, p.Value);
            }
            else
            {
                conf.Dependencies.Add(dep);
            }
            var shared = configProvider.GetSharedConfig();
            if (shared != null)
                shared.Config = conf;
            OnConfigDependencyAdded?.Invoke(this, conf, dep, existing != null);
            configProvider.Commit();
            // Perform additional modification
            OnDependencyAdded?.Invoke(this, dep, existing != null);
        }

        public bool RemoveDependency(string matchingId)
        {
            // If the given dependency exists, remove it
            // If it doesn't, return false
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotFound);
            // Get matching dependency, there should only be one per each id
            var matchingDep = conf.Dependencies.FirstOrDefault(d => matchingId.Equals(d.Id, StringComparison.OrdinalIgnoreCase));
            if (matchingDep is null)
                return false;
            // Ids are not case sensitive
            var result = conf.Dependencies.Remove(matchingDep);
            if (result)
            {
                OnConfigDependencyRemoved?.Invoke(this, conf, matchingDep);
                // Get local config to remove dependency from IncludedDependencies if it exists
                var sharedConf = configProvider.GetSharedConfig();
                if (sharedConf != null)
                    sharedConf.RestoredDependencies.RemoveAll(p => p.Id == matchingId.ToUpperInvariant());
                // Perform additional modification
                OnDependencyRemoved?.Invoke(this, matchingDep);
                // This happens only after OnDependencyRemoved occurrs, ensuring that throws will happen properly
                // No need to commit unless we actually changed the config with a successful removal
                // We commit this change to both our config and shared config objects.
                var shared = configProvider.GetSharedConfig();
                if (shared != null)
                    shared.Config = conf;
                configProvider.Commit();
            }
            return result;
        }
    }
}