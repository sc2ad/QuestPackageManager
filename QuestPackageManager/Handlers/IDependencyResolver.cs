using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPackageManager
{
    public interface IDependencyResolver
    {
        public Config? GetConfig(Dependency dependency);

        public void ResolveDependency(in Config myConfig, in Dependency dependency);
    }
}