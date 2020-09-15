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
        public void Simple()
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
            Assert.Collection(deps,
                kvp =>
                {
                    Assert.True(kvp.Key.Dependency.Id == dep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == dep.VersionRange);
                });
        }

        [Fact]
        public void NestedNew()
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
            // We should now have TWO dependencies, one for id and one for id2
            // Order of these dependencies should not matter. They just both need to be in there.
            Assert.Collection(deps,
                kvp =>
                {
                    Assert.True(kvp.Key.Dependency.Id == dep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == dep.VersionRange);
                    Assert.True(kvp.Value.Config.Info.Version == depConfig.Config.Info.Version);
                },
                kvp =>
                {
                    Assert.True(kvp.Key.Dependency.Id == innerDep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == innerDep.VersionRange);
                    Assert.True(kvp.Value.Config.Info.Version == innerDepConfig.Config.Info.Version);
                });
        }

        [Fact]
        public void NestedExistingUnique()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"));
            var otherDep = new Dependency("needed", new SemVer.Range("^0.1.4"));
            config.Dependencies.Add(dep);
            config.Dependencies.Add(otherDep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) } };
            var innerDep = new Dependency("needed", new SemVer.Range("^0.1.0"));
            depConfig.Config.Dependencies.Add(innerDep);

            //depConfig.RestoredDependencies.Add(new RestoredDependencyPair { Dependency = innerDep, Version = new SemVer.Version("0.1.4") });
            var otherDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.4")) } };
            var innerDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.3")) } };

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
        }

        [Fact]
        public void NestedExistingDuplicate()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("id", new SemVer.Range("^0.1.0"));
            var otherDep = new Dependency("needed", new SemVer.Range("^0.1.4"));
            config.Dependencies.Add(dep);
            config.Dependencies.Add(otherDep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "id", new SemVer.Version("0.1.0")) } };
            var innerDep = new Dependency("needed", new SemVer.Range("^0.1.0"));
            depConfig.Config.Dependencies.Add(innerDep);

            //depConfig.RestoredDependencies.Add(new RestoredDependencyPair { Dependency = innerDep, Version = new SemVer.Version("0.1.4") });
            var innerDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("Needed by both", "needed", new SemVer.Version("0.1.4")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig>
            {
                { dep, depConfig }, { otherDep, innerDepConfig }, { innerDep, innerDepConfig }
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
            // We should now ONLY HAVE TWO dependencies!
            // The two dependencies for "needed" should be auto-collapsed into one, because the Versions match
            Assert.Collection(deps,
                kvp =>
                {
                    // Dependency must match, so should the version range (the first dependency should be from ourselves, so it should match perfectly)
                    Assert.True(kvp.Key.Dependency.Id == dep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == dep.VersionRange);
                    // Config version must be the correctly resolved version
                    Assert.True(kvp.Value.Config.Info.Version == depConfig.Config.Info.Version);
                },
                kvp =>
                {
                    // Dependency ID must match, version range doesn't matter, but theoretically should intersect
                    Assert.True(kvp.Key.Dependency.Id == otherDep.Id);
                    // Config version must be the correctly resolved version
                    Assert.True(kvp.Value.Config.Info.Version == innerDepConfig.Config.Info.Version);
                }
            );
        }

        [Fact]
        public void Recursive()
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
        public void NestedRecursive()
        {
            var config = new Config() { Info = new PackageInfo("MyMod", "id", new SemVer.Version("0.1.0")) };
            var dep = new Dependency("asdf", new SemVer.Range("^0.1.0"));
            config.Dependencies.Add(dep);
            var depConfig = new SharedConfig { Config = new Config() { Info = new PackageInfo("Cool Name", "asdf", new SemVer.Version("0.1.0")) } };
            // It's undefined behavior to attempt to load a config that allows its dependencies to ask for itself
            // Therefore, we will test ourselves, and all other configs must follow this same principle
            var innerDep = new Dependency("id", new SemVer.Range("^0.1.0"));
            depConfig.Config.Dependencies.Add(innerDep);
            var innerDepConfig = new SharedConfig { Config = new Config { Info = new PackageInfo("inner", "id", new SemVer.Version("0.1.0")) } };

            var configProvider = Utils.GetConfigProvider(config);
            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig> { { dep, depConfig }, { innerDep, innerDepConfig } });

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
            uriHandler.Verify(mocks => mocks.GetSharedConfig(It.Is<RestoredDependencyPair>(p => p.Dependency == innerDep)), Times.Once);
            // Assume it restored
            depConfig.RestoredDependencies.Add(new RestoredDependencyPair { Dependency = innerDep, Version = innerDepConfig.Config.Info.Version });

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // Should throw a recursive exception (id cannot include id)
            Assert.Throws<DependencyException>(() => restorer.CollectDependencies());
            // Should have tried to get asdf's config
            uriHandler.Verify(mocks => mocks.GetSharedConfig(It.Is<RestoredDependencyPair>(p => p.Dependency == dep)), Times.Once);
            // The inner dependency should have been attempted to have been gotten exactly once, from the innerRestorer collection
            // It should not have been collected again in our collection of dependencies
            uriHandler.Verify(mocks => mocks.GetSharedConfig(It.Is<RestoredDependencyPair>(p => p.Dependency == innerDep)), Times.Once);
        }

        [Fact]
        public void DoNotMatchConfig()
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

            var innerRestorer = new RestoreHandler(Utils.GetConfigProvider(depConfig.Config).Object, uriHandler.Object);
            // Should throw
            Assert.Throws<DependencyException>(() => innerRestorer.CollectDependencies());
            // Assume it DID NOT restore

            var restorer = new RestoreHandler(configProvider.Object, uriHandler.Object);
            // This should NOT throw, since the version we test here actually exists and is satisfiable.
            // and our dependency config DOES NOT contain a restored dependency that causes any problems.
            // TODO: We could add a test here to test for when one matches but they other doesn't too.
            var deps = restorer.CollectDependencies();
            Assert.Collection(deps,
                kvp =>
                {
                    Assert.True(kvp.Key.Dependency.Id == dep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == dep.VersionRange);
                    Assert.True(kvp.Value.Config.Info.Version == depConfig.Config.Info.Version);
                },
                kvp =>
                {
                    Assert.True(kvp.Key.Dependency.Id == otherDep.Id);
                    Assert.True(kvp.Key.Dependency.VersionRange == otherDep.VersionRange);
                    Assert.True(kvp.Value.Config.Info.Version == innerDepConfig.Config.Info.Version);
                });
        }
    }
}