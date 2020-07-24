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
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
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

        [Fact]
        public void CollectDependenciesNestedExisting()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"), new Uri("http://someLocation.com"));
            var otherDep = new Dependency("needed", new SemVer.Range("^0.1.4"), new Uri("http://random.com"));
            config.Dependencies.Add(dep);
            config.Dependencies.Add(otherDep);
            var depConfig = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) };
            var innerDep = new Dependency("needed", new SemVer.Range("^0.1.0"), new Uri("http://random.com"));
            depConfig.Dependencies.Add(innerDep);
            var innerDepConfig = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.4")) };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, Config>
            {
                { dep, depConfig }, { otherDep, innerDepConfig }, { innerDep, innerDepConfig }
            });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            // We should now only have TWO dependencies, one for "id" and one for "needed"
            // The dependency for "needed" should have a specific version range of "^0.1.4" and not "^0.1.0"
            Assert.True(deps.Count == 2);
            Assert.NotNull(deps.FirstOrDefault(d => d.Id == dep.Id && d.Url == dep.Url && d.VersionRange == dep.VersionRange));
            Assert.NotNull(deps.FirstOrDefault(d => d.Id == innerDep.Id && d.Url == innerDep.Url && d.VersionRange == otherDep.VersionRange));
        }

        [Fact]
        public void CollectDependenciesRecursive()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "id", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"), new Uri("http://someLocation.com"));
            config.Dependencies.Add(dep);
            var depConfig = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, Config> { { dep, depConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should throw a recursive exception (id cannot include id)
            Assert.Throws<DependencyException>(() => restorer.CollectDependencies());
            // Should never have made any GetConfig calls
            uriHandler.Verify(mocks => mocks.GetConfig(It.IsAny<Dependency>()), Times.Never);
        }

        [Fact]
        public void CollectDependenciesNestedRecursive()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "id", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("asdf", new SemVer.Range("^0.1.0"), new Uri("http://someLocation.com"));
            config.Dependencies.Add(dep);
            var depConfig = new Config() { Info = new PackageInfo("Cool Name", "asdf", new SemVer.Version("0.1.0")) };
            // It's undefined behavior to attempt to load a config that allows its dependencies to ask for itself
            // Therefore, we will test ourselves, and all other configs must follow this same principle
            var innerDep = new Dependency("id", new SemVer.Range("^0.1.0"), new Uri("http://test.com"));
            depConfig.Dependencies.Add(innerDep);
            var innerDepConfig = new Config();

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, Config> { { dep, depConfig }, { innerDep, innerDepConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should throw a recursive exception (id cannot include id)
            Assert.Throws<DependencyException>(() => restorer.CollectDependencies());
            // Should have tried to get asdf's config
            uriHandler.Verify(mocks => mocks.GetConfig(dep), Times.Once);
            uriHandler.Verify(mocks => mocks.GetConfig(innerDep), Times.Never);
        }

        [Fact]
        public void CollectDependenciesDoNotMatchSimple()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"), new Uri("http://someLocation.com"));
            config.Dependencies.Add(dep);
            var depConfig = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.0.1")) };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, Config> { { dep, depConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);

            Assert.Contains("id", Assert.Throws<DependencyException>(() => restorer.CollectDependencies()).Message);
        }

        [Fact]
        public void CollectDependenciesDoNotMatchNested()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"), new Uri("http://someLocation.com"));
            config.Dependencies.Add(dep);
            var depConfig = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.1")) };
            var innerDep = new Dependency("id2", new SemVer.Range("^0.1.0"), new Uri("http://random.com"));
            depConfig.Dependencies.Add(innerDep);
            var innerDepConfig = new Config { Info = new PackageInfo("Cool Name", "id2", new SemVer.Version("0.0.1")) };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, Config> { { dep, depConfig }, { innerDep, innerDepConfig } });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);

            Assert.Contains("id2", Assert.Throws<DependencyException>(() => restorer.CollectDependencies()).Message);
        }

        [Fact]
        public void CollectDependenciesDoNotMatchExistingNested()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"), new Uri("http://someLocation.com"));
            var otherDep = new Dependency("needed", new SemVer.Range("^0.1.4"), new Uri("http://random.com"));
            config.Dependencies.Add(dep);
            config.Dependencies.Add(otherDep);
            var depConfig = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) };
            var innerDep = new Dependency("needed", new SemVer.Range("^0.1.0"), new Uri("http://random.com"));
            depConfig.Dependencies.Add(innerDep);
            var innerDepConfig = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.0.4")) };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, Config>
            {
                { dep, depConfig }, { otherDep, innerDepConfig }, { innerDep, innerDepConfig }
            });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);

            Assert.Contains("needed", Assert.Throws<DependencyException>(() => restorer.CollectDependencies()).Message);
        }

        [Fact]
        public void CollectDependenciesDoNotMatchRangeExistingNested()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"), new Uri("http://someLocation.com"));
            var otherDep = new Dependency("needed", new SemVer.Range("^0.2.4"), new Uri("http://random.com"));
            config.Dependencies.Add(dep);
            config.Dependencies.Add(otherDep);
            var depConfig = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) };
            var innerDep = new Dependency("needed", new SemVer.Range("^0.1.0"), new Uri("http://random.com"));
            depConfig.Dependencies.Add(innerDep);
            var innerDepConfig = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.4")) };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, Config>
            {
                { dep, depConfig }, { otherDep, innerDepConfig }, { innerDep, innerDepConfig }
            });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);

            Assert.Contains("needed", Assert.Throws<DependencyException>(() => restorer.CollectDependencies()).Message);
        }
    }
}