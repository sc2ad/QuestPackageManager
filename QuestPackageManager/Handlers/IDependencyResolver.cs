using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuestPackageManager
{
    public interface IDependencyResolver
    {
        public Task<SharedConfig?> GetSharedConfig(RestoredDependencyPair pairWithVersion);

        public Task ResolveDependency(Config myConfig, RestoredDependencyPair dependency);

        public Task ResolveUniqueDependency(Config myConfig, KeyValuePair<RestoredDependencyPair, SharedConfig> resolved);

        public void RemoveDependency(in Config myConfig, in Dependency dependency);
    }
}