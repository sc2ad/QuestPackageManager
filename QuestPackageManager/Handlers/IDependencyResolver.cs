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
        public SharedConfig? GetSharedConfig(Dependency dependency);

        public void ResolveDependency(in Config myConfig, in Dependency dependency);

        public void ResolveUniqueDependency(in Config myConfig, in (Dictionary<string, JsonElement> data, SharedConfig conf) resolved);

        public void RemoveDependency(in Config myConfig, in Dependency dependency);
    }
}