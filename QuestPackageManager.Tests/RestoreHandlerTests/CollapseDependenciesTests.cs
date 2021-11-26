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
        public async Task SimpleCollapse()
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
            var deps = await restorer.CollectDependencies();
            var result = RestoreHandler.CollapseDependencies(deps);
            Assert.All(deps, kvp =>
            {
                Assert.True(result.TryGetValue(kvp.Key, out var conf));
                Assert.True(kvp.Value == conf);
            });
        }

        [Fact]
        public async Task NestedCollapse()
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
            var deps = await restorer.CollectDependencies();
            var result = RestoreHandler.CollapseDependencies(deps);
            Assert.All(deps, kvp =>
            {
                Assert.True(result.TryGetValue(kvp.Key, out var conf));
                Assert.True(kvp.Value == conf);
            });
        }

        [Fact]
        public async Task CollapseMatching()
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
            var innerDeps = await innerRestorer.CollectDependencies();
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
            var deps = await restorer.CollectDependencies();
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

        private Dependency MakeDep(string id, string range) => new(id, new SemVer.Range(range));
        private SharedConfig MakeCfg(string id, string ver, string name = "") => new() { Config = new Config() { Info = new PackageInfo(name, id, new SemVer.Version(ver)) } };
        private RestoredDependencyPair MakeRestoredPair(Dependency dep, string ver) => new() { Dependency = dep, Version = new SemVer.Version(ver) };

        private static Action<KeyValuePair<RestoredDependencyPair, SharedConfig>> Match(Dependency depToMatch, SharedConfig config)
        {
            return kvp =>
            {
                Assert.Equal(depToMatch.Id, kvp.Key.Dependency.Id);
                Assert.True(depToMatch.VersionRange.IsSatisfied(kvp.Key.Version));
                Assert.Equal(config.Config.Info.Version, kvp.Value.Config.Info.Version);
            };
        }

        [Fact]
        public async Task CollapseWithEquality()
        {
            // Collect dependencies that have a * and = in them and make sure they collect + collapse to the =
            var config = new Config { Info = new PackageInfo("MyMod", "asdf", new SemVer.Version("0.1.0")) };
            // Some deps:
            // 1 is top level, =
            // 2 is nested with top level dep, ^
            // 3 is nested with top level dep, *
            var d1 = MakeDep("id", "=0.1.1");
            var d2 = MakeDep("id2", "^0.1.0");
            var d3 = MakeDep("id3", "^0.1.0");
            config.Dependencies.AddRange(new [] { d1, d2, d3 });
            // In order of appearance:
            var idCfg = MakeCfg("id", "0.1.1");

            var carrotCfg = MakeCfg("id2", "0.1.0");
            var innerCarrot = MakeDep("id", "^0.1.0");
            carrotCfg.Config.Dependencies.Add(innerCarrot);
            carrotCfg.RestoredDependencies.Add(MakeRestoredPair(innerCarrot, "0.1.2"));

            var exactCfg = MakeCfg("id3", "0.1.0");
            var innerStar = MakeDep("id", "*");
            exactCfg.Config.Dependencies.Add(innerStar);
            exactCfg.RestoredDependencies.Add(MakeRestoredPair(innerStar, "0.2.0"));

            // Now we make the id versions: 0.1.2, 0.2.0
            var id2Cfg = MakeCfg("id", "0.1.2");
            var id3Cfg = MakeCfg("id", "0.2.0");

            var uriHandler = Utils.GetUriHandler(new Dictionary<Dependency, SharedConfig>
            {
                { d1, idCfg }, { d2, carrotCfg }, { d3, exactCfg }, { innerCarrot, id2Cfg }, { innerStar, id3Cfg }
            });

            // Should not throw
            var restorer = new RestoreHandler(Utils.GetConfigProvider(config).Object, uriHandler.Object);
            var deps = await restorer.CollectDependencies();

            var collapsed = RestoreHandler.CollapseDependencies(deps);
            // The only resolved unique id SHOULD be: 0.1.1, because it is the only one with an exact version match
            // All others INTERSECT with the version range, but should not OVERWRITE it.
            Assert.Collection(collapsed,
                Match(d1, idCfg),
                Match(d2, carrotCfg),
                Match(d3, exactCfg));
        }

        [Fact]
        public async Task CollapseInvalid()
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
            var innerDeps = await innerRestorer.CollectDependencies();
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
            var deps = await restorer.CollectDependencies();
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