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
        public SharedConfig? GetSharedConfig(RestoredDependencyPair pairWithVersion);

        public void ResolveDependency(in Config myConfig, in RestoredDependencyPair dependency);

        public void ResolveUniqueDependency(in Config myConfig, in (Dictionary<string, JsonElement> data, SharedConfig conf) resolved);

        public void RemoveDependency(in Config myConfig, in Dependency dependency);
    }
}