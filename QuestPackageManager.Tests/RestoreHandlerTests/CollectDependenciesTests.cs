using Moq;
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
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"));
            config.Dependencies.Add(dep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.1")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig> { { dep, depConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            // Ensure we still only have one dependency and nothing has changed
            var item = deps.Keys.SingleOrDefault();
            Assert.True(item != null);
            var checkD = item.Dependency;
            Assert.True(checkD != null);
            Assert.True(checkD.Id == dep.Id);
            Assert.True(checkD.VersionRange == dep.VersionRange);
        }

        [Fact]
        public void CollectDependenciesNestedNew()
        {
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
            // We should now have TWO dependencies, one for id and one for id2
            // Order of these dependencies should not matter. They just both need to be in there.
            Assert.True(deps.Count == 2);
            Assert.NotNull(deps.Keys.FirstOrDefault(d => d.Dependency.Id == dep.Id && d.Dependency.VersionRange == dep.VersionRange));
            Assert.NotNull(deps.Keys.FirstOrDefault(d => d.Dependency.Id == innerDep.Id && d.Dependency.VersionRange == innerDep.VersionRange));
        }

        [Fact]
        public void CollectDependenciesNestedExisting()
        {
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
            // We should now STILL HAVE THREE dependencies, one for "id" and two for "needed"
            Assert.True(deps.Count == 3);
            Assert.NotNull(deps.Keys.FirstOrDefault(d => d.Dependency.Id == dep.Id && d.Dependency.VersionRange == dep.VersionRange));
            Assert.NotNull(deps.Keys.FirstOrDefault(d => d.Dependency.Id == otherDep.Id && d.Dependency.VersionRange == otherDep.VersionRange));
            Assert.NotNull(deps.Keys.LastOrDefault(d => d.Dependency.Id == innerDep.Id && d.Dependency.VersionRange == innerDep.VersionRange));
        }

        [Fact]
        public void CollectDependenciesRecursive()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "id", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"));
            config.Dependencies.Add(dep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig> { { dep, depConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should throw a recursive exception (id cannot include id)
            Assert.Throws<DependencyException>(() => restorer.CollectDependencies());
            // Should never have made any GetConfig calls
            uriHandler.Verify(mocks => mocks.GetSharedConfig(It.IsAny<RestoredDependencyPair>()), Times.Never);
        }

        [Fact]
        public void CollectDependenciesNestedRecursive()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "id", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("asdf", new SemVer.Range("^0.1.0"));
            config.Dependencies.Add(dep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "asdf", new SemVer.Version("0.1.0")) } };
            // It's undefined behavior to attempt to load a config that allows its dependencies to ask for itself
            // Therefore, we will test ourselves, and all other configs must follow this same principle
            var innerDep = new Dependency("id", new SemVer.Range("^0.1.0"));
            depConfig.Config.Dependencies.Add(innerDep);
            var innerDepConfig = new SharedConfig();

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig> { { dep, depConfig }, { innerDep, innerDepConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should throw a recursive exception (id cannot include id)
            Assert.Throws<DependencyException>(() => restorer.CollectDependencies());
            // Should have tried to get asdf's config
            uriHandler.Verify(mocks => mocks.GetSharedConfig(It.Is<RestoredDependencyPair>(p => p.Dependency == dep)), Times.Once);
            uriHandler.Verify(mocks => mocks.GetSharedConfig(It.Is<RestoredDependencyPair>(p => p.Dependency == innerDep)), Times.Never);
        }

        [Fact]
        public void CollectDependenciesDoNotMatchConfig()
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
            var innerDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.4")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig>
            {
                { dep, depConfig }, { otherDep, innerDepConfig }, { innerDep, innerDepConfig }
            });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            Assert.Throws<DependencyException>(() => restorer.CollectDependencies());
        }
    }
}