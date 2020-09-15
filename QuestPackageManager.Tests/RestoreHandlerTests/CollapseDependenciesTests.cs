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
        public void SimpleCollapse()
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
            Assert.All(deps, kvp =>
            {
                Assert.True(result.TryGetValue(kvp.Key, out var conf));
                Assert.True(kvp.Value == conf);
            });
        }

        [Fact]
        public void NestedCollapse()
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
            Assert.All(deps, kvp =>
            {
                Assert.True(result.TryGetValue(kvp.Key, out var conf));
                Assert.True(kvp.Value == conf);
            });
        }

        [Fact]
        public void CollapseMatching()
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
            var otherDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.4")) } };
            var innerDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.2")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig>
            {
                { dep, depConfig }, { otherDep, otherDepConfig }, { innerDep, innerDepConfig }
            });

            var innerRestorer = new RestoreHandler(Utils.GetConfigProvider(depConfig.Config).Object, uriHandler.Object);
            // Should not throw
            var innerDeps = innerRestorer.CollectDependencies();
            Assert.Collection(innerDeps,
                kvp =>
                {
                    Assert.True(kvp.Key.Dependency.Id == innerDep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == innerDep.VersionRange);
                    Assert.True(kvp.Value.Config.Info.Version == innerDepConfig.Config.Info.Version);
                }
            );
            // Assume it restored
            depConfig.RestoredDependencies.Add(new RestoredDependencyPair { Dependency = innerDep, Version = innerDepConfig.Config.Info.Version });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            // We should now HAVE THREE dependencies!
            // The two dependencies for "needed" (because they are unique versions) and the dependency for "id"
            Assert.Collection(deps,
                kvp =>
                {
                    // Dependency ID and VersionRange must match
                    // This is because the first dependency must be the one from our config
                    Assert.True(kvp.Key.Dependency.Id == dep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == dep.VersionRange);
                    // Config version must be the correctly resolved version
                    Assert.True(kvp.Value.Config.Info.Version == depConfig.Config.Info.Version);
                },
                kvp =>
                {
                    // Dependency ID must match
                    Assert.True(kvp.Key.Dependency.Id == innerDep.Id);
                    // Config version must be the correctly resolved version
                    Assert.True(kvp.Value.Config.Info.Version == innerDepConfig.Config.Info.Version);
                },
                kvp =>
                {
                    // Dependency ID must match
                    Assert.True(kvp.Key.Dependency.Id == otherDep.Id);
                    // Config version must be the correctly resolved version
                    Assert.True(kvp.Value.Config.Info.Version == otherDepConfig.Config.Info.Version);
                }
            );
            var result = RestoreHandler.CollapseDependencies(deps);
            // After collapsing, we should have the exact same version.
            // This is an easy case, since their config objects are literally identical.
            Assert.Collection(result,
                kvp =>
                {
                    // Dependency ID and VersionRange must match
                    // This is because the first dependency must be the one from our config
                    Assert.True(kvp.Key.Dependency.Id == dep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == dep.VersionRange);
                    // Config version must be the correctly resolved version
                    Assert.True(kvp.Value.Config.Info.Version == depConfig.Config.Info.Version);
                },
                kvp =>
                {
                    // Dependency ID must match
                    Assert.True(kvp.Key.Dependency.Id == innerDep.Id);
                    // Config version should be the HIGHEST version. In this case, otherDepConfig
                    Assert.True(kvp.Value.Config.Info.Version == otherDepConfig.Config.Info.Version);
                }
            );
        }

        [Fact]
        public void CollapseInvalid()
        {
            // Collect nested dependencies that are not collapsible, should cause a DependencyException
            // Collect nested dependencies that are collapsible, should result in 1
            var config = new Config { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"));
            var otherDep = new Dependency("needed", new SemVer.Range("^0.1.4"));
            config.Dependencies.Add(dep);
            config.Dependencies.Add(otherDep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) } };
            var innerDep = new Dependency("needed", new SemVer.Range("=0.1.0"));
            depConfig.Config.Dependencies.Add(innerDep);
            var otherDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.4")) } };
            var innerDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.0")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig>
            {
                { dep, depConfig }, { otherDep, otherDepConfig }, { innerDep, innerDepConfig }
            });

            var innerRestorer = new RestoreHandler(Utils.GetConfigProvider(depConfig.Config).Object, uriHandler.Object);
            // Should not throw
            var innerDeps = innerRestorer.CollectDependencies();
            Assert.Collection(innerDeps,
                kvp =>
                {
                    Assert.True(kvp.Key.Dependency.Id == innerDep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == innerDep.VersionRange);
                    Assert.True(kvp.Value.Config.Info.Version == innerDepConfig.Config.Info.Version);
                }
            );
            // Assume it restored
            depConfig.RestoredDependencies.Add(new RestoredDependencyPair { Dependency = innerDep, Version = innerDepConfig.Config.Info.Version });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should not throw
            var deps = restorer.CollectDependencies();
            // We should now HAVE THREE dependencies!
            // The two dependencies for "needed" (because they are unique versions) and the dependency for "id"
            Assert.Collection(deps,
                kvp =>
                {
                    // Dependency ID and VersionRange must match
                    // This is because the first dependency must be the one from our config
                    Assert.True(kvp.Key.Dependency.Id == dep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == dep.VersionRange);
                    // Config version must be the correctly resolved version
                    Assert.True(kvp.Value.Config.Info.Version == depConfig.Config.Info.Version);
                },
                kvp =>
                {
                    // Dependency ID must match
                    Assert.True(kvp.Key.Dependency.Id == innerDep.Id);
                    // Config version must be the correctly resolved version
                    Assert.True(kvp.Value.Config.Info.Version == innerDepConfig.Config.Info.Version);
                },
                kvp =>
                {
                    // Dependency ID must match
                    Assert.True(kvp.Key.Dependency.Id == otherDep.Id);
                    // Config version must be the correctly resolved version
                    Assert.True(kvp.Value.Config.Info.Version == otherDepConfig.Config.Info.Version);
                }
            );
            Assert.Throws<DependencyException>(() => RestoreHandler.CollapseDependencies(deps));
        }
    }
}