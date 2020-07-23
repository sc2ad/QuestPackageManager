using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace QuestPackageManager.Tests.RestoreHandlerTests
{
    public class CollectDependenciesTests
    {
        [Fact]
        public void CollectDependenciesSimple()
        {
            var config = new Config();
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"), new Uri("http://someLocation.com"));
            config.Dependencies.Add(dep);
            var depConfig = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.1")) };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, Config> { { dep, depConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            // Ensure we still only have one dependency and nothing has changed
            var checkD = deps.SingleOrDefault();
            Assert.True(checkD != null);
            Assert.True(checkD.Id == dep.Id);
            Assert.True(checkD.Url == dep.Url);
            Assert.True(checkD.VersionRange == dep.VersionRange);
        }

        [Fact]
        public void CollectDependenciesNestedNew()
        {
            var config = new Config();
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"), new Uri("http://someLocation.com"));
            config.Dependencies.Add(dep);
            var depConfig = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.1")) };
            var innerDep = new Dependency("id2", new SemVer.Range("^0.1.0"), new Uri("http://random.com"));
            depConfig.Dependencies.Add(innerDep);
            var innerDepConfig = new Config { Info = new PackageInfo("Cool Name", "id2", new SemVer.Version("0.1.1")) };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, Config> { { dep, depConfig }, { innerDep, innerDepConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            // We should now have TWO dependencies, one for id and one for id2
            // Order of these dependencies should not matter. They just both need to be in there.
            Assert.True(deps.Count == 2);
            Assert.NotNull(deps.FirstOrDefault(d => d.Id == dep.Id && d.Url == dep.Url && d.VersionRange == dep.VersionRange));
            Assert.NotNull(deps.FirstOrDefault(d => d.Id == innerDep.Id && d.Url == innerDep.Url && d.VersionRange == innerDep.VersionRange));
        }
    }
}