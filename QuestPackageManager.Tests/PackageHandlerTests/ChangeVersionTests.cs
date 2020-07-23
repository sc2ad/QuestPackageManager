using Moq;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace QuestPackageManager.Tests.PackageHandlerTests
{
    public class ChangeVersionTests
    {
        [Fact]
        public void ChangeVersionExceptions()
        {
            // Callbacks
            bool configCalled = false;
            void Handler_OnConfigVersionChanged(PackageHandler arg1, Config config, SemVer.Version arg2)
            {
                configCalled = true;
            }
            bool called = false;
            void Handler_OnVersionChanged(PackageHandler arg1, SemVer.Version arg2)
            {
                called = true;
            }

            var config = new Config { Info = new PackageInfo("N", "ID", new SemVer.Version("0.0.1")) };
            var configProvider = Utils.GetConfigProvider(config, failToGet: true);

            var handler = new PackageHandler(configProvider.Object);
            handler.OnConfigVersionChanged += Handler_OnConfigVersionChanged;
            handler.OnVersionChanged += Handler_OnVersionChanged;

            // Should throw an ANE for a null version
            Assert.Throws<ArgumentNullException>(() => handler.ChangeVersion(null));

            // Should throw a failure if the config could not be found
            var newVersion = new SemVer.Version("0.1.0");
            Assert.Throws<ConfigException>(() => handler.ChangeVersion(newVersion));
            // Config should never have been committed or changed
            configProvider.Verify(mocks => mocks.Commit(), Times.Never);
            Assert.False(config.Info.Version == newVersion);
            // Callbacks should never have been called
            Assert.False(configCalled);
            Assert.False(called);

            config = new Config();
            configProvider = Utils.GetConfigProvider(config);
            handler = new PackageHandler(configProvider.Object);
            handler.OnConfigVersionChanged += Handler_OnConfigVersionChanged;
            handler.OnVersionChanged += Handler_OnVersionChanged;

            // Should throw a failure if the config.Info property is null
            Assert.Throws<ConfigException>(() => handler.ChangeVersion(newVersion));

            // Config should never have been committed
            configProvider.Verify(mocks => mocks.Commit(), Times.Never);
            // Callbacks should never have been called
            Assert.False(configCalled);
            Assert.False(called);
        }

        [Fact]
        public void ChangeVersionStandard()
        {
            // Callbacks
            bool configCalled = false;
            void Handler_OnConfigVersionChanged(PackageHandler arg1, Config config, SemVer.Version arg2)
            {
                configCalled = true;
            }
            bool called = false;
            void Handler_OnVersionChanged(PackageHandler arg1, SemVer.Version arg2)
            {
                called = true;
            }

            var config = new Config { Info = new PackageInfo("N", "ID", new SemVer.Version("0.0.1")) };
            var configProvider = Utils.GetConfigProvider(config);

            var handler = new PackageHandler(configProvider.Object);
            handler.OnConfigVersionChanged += Handler_OnConfigVersionChanged;
            handler.OnVersionChanged += Handler_OnVersionChanged;

            var newVersion = new SemVer.Version("0.1.0");
            handler.ChangeVersion(newVersion);

            // Ensure config was committed
            configProvider.Verify(m => m.Commit());
            // Ensure config has changed to match info
            Assert.True(config.Info.Version == newVersion);
            // Ensure callbacks were triggered
            Assert.True(configCalled);
            Assert.True(called);
        }

        [Fact]
        public void ChangeVersionPluginException()
        {
            static void Handler_OnConfigVersionChanged(PackageHandler arg1, Config config, SemVer.Version arg2)
            {
                throw new ArgumentOutOfRangeException();
            }
            var config = new Config { Info = new PackageInfo("N", "ID", new SemVer.Version("0.0.1")) };
            var configProvider = Utils.GetConfigProvider(config);

            var handler = new PackageHandler(configProvider.Object);
            handler.OnConfigVersionChanged += Handler_OnConfigVersionChanged;

            var newVersion = new SemVer.Version("0.1.0");

            // Should throw plugin exception
            Assert.Throws<ArgumentOutOfRangeException>(() => handler.ChangeVersion(newVersion));
            // Ensure config has not been committed
            configProvider.Verify(mocks => mocks.Commit(), Times.Never);
            // Ensure config has not changed
            Assert.False(config.Info.Version == newVersion);
        }
    }
}