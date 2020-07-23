using Moq;
using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPackageManager.Tests
{
    internal static class Utils
    {
        internal static Mock<IConfigProvider> GetConfigProvider(Config config, bool failToCreate = false, bool failToGet = false)
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

        internal static Mock<IUriHandler> GetUriHandler(Dictionary<Dependency, Config> map)
        {
            var mock = new Mock<IUriHandler>();
            mock.Setup(m => m.GetConfig(It.IsAny<Dependency>())).Returns<Dependency>(d => map[d]);
            mock.Setup(m => m.ResolveDependency(It.IsAny<Dependency>()));
            return mock;
        }
    }
}