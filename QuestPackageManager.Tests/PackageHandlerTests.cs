using Moq;
using QuestPackageManager.Data;
using System;
using Xunit;

namespace QuestPackageManager.Tests
{
    public class PackageHandlerTests
    {
        [Fact]
        public void TestCreatePackageStandard()
        {
            // Callbacks
            bool calledConfigured = false;
            void Handler_OnPackageConfigured(PackageHandler arg1, Config arg2, PackageInfo arg3)
            {
                calledConfigured = true;
            }
            bool calledCreated = false;
            void Handler_OnPackageCreated(PackageHandler arg1, PackageInfo arg3)
            {
                calledCreated = true;
            }

            // Start with an empty config
            var config = new Config();
            var configProvider = GetConfigProvider(config);

            var handler = new PackageHandler(configProvider.Object);

            var info = new PackageInfo("CoolName", "CoolId", new SemVer.Version("0.1.0")) { Url = new Uri("http://test.com") };
            handler.OnPackageConfigured += Handler_OnPackageConfigured;
            handler.OnPackageCreated += Handler_OnPackageCreated;
            handler.CreatePackage(info);

            // Ensure config was created
            configProvider.Verify(m => m.GetConfig(true));
            // Ensure config was committed
            configProvider.Verify(m => m.Commit());
            // Ensure config has changed to match info
            Assert.True(config.Info.Id == info.Id);
            Assert.True(config.Info.Name == info.Name);
            Assert.True(config.Info.Version == info.Version);
            Assert.True(config.Info.Url == info.Url);
            // Ensure callbacks were triggered
            Assert.True(calledConfigured);
            Assert.True(calledCreated);
        }

        [Fact]
        public void TestCreatePackageExceptions()
        {
            // Callbacks
            bool calledConfigured = false;
            void Handler_OnPackageConfigured(PackageHandler arg1, Config arg2, PackageInfo arg3)
            {
                calledConfigured = true;
            }
            bool calledCreated = false;
            void Handler_OnPackageCreated(PackageHandler arg1, PackageInfo arg3)
            {
                calledCreated = true;
            }

            var config = new Config();
            var configProvider = GetConfigProvider(config, true);

            var handler = new PackageHandler(configProvider.Object);
            handler.OnPackageConfigured += Handler_OnPackageConfigured;
            handler.OnPackageCreated += Handler_OnPackageCreated;

            var info = new PackageInfo("CoolName", "CoolId", new SemVer.Version("0.1.0")) { Url = new Uri("http://test.com") };
            // Ensure a ConfigException is thrown
            Assert.Throws<ConfigException>(() => handler.CreatePackage(info));
            // Ensure config has not been committed
            configProvider.Verify(m => m.Commit(), Times.Never);
            // Ensure config has not changed
            Assert.True(config.Info is null);
            // Ensure callbacks did not happen
            Assert.False(calledConfigured);
            Assert.False(calledCreated);
        }

        private Mock<IConfigProvider> GetConfigProvider(Config config, bool failToCreate = false, bool failToGet = false)
        {
            var mock = new Mock<IConfigProvider>();
            mock.Setup(m => m.Commit()).Verifiable();
            if (failToCreate)
                mock.Setup(m => m.GetConfig(true)).Returns<Config>(null);
            else
                mock.Setup(m => m.GetConfig(true)).Returns(config);
            if (failToGet)
                mock.Setup(m => m.GetConfig(false)).Returns<Config>(null);
            else
                mock.Setup(m => m.GetConfig(false)).Returns(config);
            return mock;
        }
    }
}