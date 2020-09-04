using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace QuestPackageManager.Tests.RestoreHandlerTests
{
    public class CollapseDependenciesTests
    {
        [Fact]
        public void TestSimpleCollapse()
        {
            // Collect a dependency, collapse it, and nothing should change
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"));
            config.Dependencies.Add(dep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.1")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig> { { dep, depConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            var result = RestoreHandler.CollapseDependencies(deps);
            foreach (var kvp in deps)
                Assert.True(result.TryGetValue(kvp.Key.Id!.ToUpperInvariant(), out var val) && kvp.Value == val.conf);
        }

        [Fact]
        public void TestNestedCollapse()
        {
            // Collect nested dependencies, collapse them, nothing should change
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"));
            config.Dependencies.Add(dep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.1")) } };
            var innerDep = new Dependency("id2", new SemVer.Range("^0.1.0"));
            depConfig.Config.Dependencies.Add(innerDep);
            var innerDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Cool Name", "id2", new SemVer.Version("0.1.1")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig> { { dep, depConfig }, { innerDep, innerDepConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            var result = RestoreHandler.CollapseDependencies(deps);
            foreach (var kvp in deps)
                Assert.True(result.TryGetValue(kvp.Key.Id!.ToUpperInvariant(), out var val) && kvp.Value == val.conf);
        }

        [Fact]
        public void TestCollapseMatching()
        {
            // Collect nested dependencies that are collapsible, should result in 1
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"));
            var otherDep = new Dependency("needed", new SemVer.Range("^0.1.4"));
            config.Dependencies.Add(dep);
            config.Dependencies.Add(otherDep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) } };
            var innerDep = new Dependency("needed", new SemVer.Range("^0.1.0"));
            depConfig.Config.Dependencies.Add(innerDep);
            var innerDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.4")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig>
            {
                { dep, depConfig }, { otherDep, innerDepConfig }, { innerDep, innerDepConfig }
            });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            var result = RestoreHandler.CollapseDependencies(deps);
            Assert.True(result.Count == 2);
            Assert.True(result[dep.Id.ToUpperInvariant()].conf.Config.Info.Version == depConfig.Config.Info.Version);
            Assert.True(result[otherDep.Id.ToUpperInvariant()].conf.Config.Info.Version == innerDepConfig.Config.Info.Version);
        }

        [Fact]
        public void TestCollapseInvalid()
        {
            // Collect nested dependencies that are not collapsible, should cause a DependencyException
            // Collect nested dependencies that are collapsible, should result in 1
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"));
            var otherDep = new Dependency("needed", new SemVer.Range("^0.1.4"));
            config.Dependencies.Add(dep);
            config.Dependencies.Add(otherDep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) } };
            var innerDep = new Dependency("needed", new SemVer.Range("0.1.0"));
            depConfig.Config.Dependencies.Add(innerDep);
            var otherDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.4")) } };
            var innerDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.0")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig>
            {
                { dep, depConfig }, { otherDep, otherDepConfig }, { innerDep, innerDepConfig }
            });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            Assert.Throws<DependencyException>(() => RestoreHandler.CollapseDependencies(deps));
        }
    }
}