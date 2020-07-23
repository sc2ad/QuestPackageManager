using QuestPackageManager.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPackageManager
{
    public class PackageHandler
    {
        private readonly IConfigProvider configProvider;

        public event Action<PackageHandler, Config, PackageInfo>? OnPackageConfigured;

        public event Action<PackageHandler, PackageInfo>? OnPackageCreated;

        public event Action<PackageHandler, Config, string>? OnConfigIdChanged;

        public event Action<PackageHandler, string>? OnIdChanged;

        public event Action<PackageHandler, Config, SemVer.Version>? OnConfigVersionChanged;

        public event Action<PackageHandler, SemVer.Version>? OnVersionChanged;

        public PackageHandler(IConfigProvider configProvider)
        {
            this.configProvider = configProvider;
        }

        public void CreatePackage(PackageInfo info)
        {
            if (info is null)
                throw new ArgumentException(Resources.Info);
            var conf = configProvider.GetConfig(true);
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotCreated);

            conf.Info = info;
            // Call extra modification as necessary
            OnPackageConfigured?.Invoke(this, conf, info);
            configProvider.Commit();
            // Perform extra modification
            OnPackageCreated?.Invoke(this, info);
            // Ex: Android.mk modification
            // Grab the config (or create it if it doesn't exist) and put the package info into it
            // Package info should contain the ID, version of this, along with a package URL (what repo this exists at) optionally empty.
            // Creating a package also ensures your Android.mk has the correct MOD_ID and VERSION set, which SHOULD be used in your main.cpp setup function.
        }

        public void ChangeUrl(Uri url)
        {
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotFound);
            if (conf.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            conf.Info.Url = url;
        }

        public void ChangeId(string id)
        {
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotFound);
            if (conf.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            conf.Info.Id = id;
            // Call extra modification as necessary
            OnConfigIdChanged?.Invoke(this, conf, id);
            configProvider.Commit();
            // Perform extra modification
            OnIdChanged?.Invoke(this, id);
            // Changes the ID of the package.
            // Grabs the config, modifies the ID, commits it
            // Changes the ID in Android.mk to match
        }

        public void ChangeVersion(SemVer.Version newVersion)
        {
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotFound);
            if (conf.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            conf.Info.Version = newVersion;
            // Call extra modification to config as necessary
            OnConfigVersionChanged?.Invoke(this, conf, newVersion);
            configProvider.Commit();
            // Perform extra modification
            OnVersionChanged?.Invoke(this, newVersion);
            // Changes the version of the package.
            // Grabs the config, modifies the version, commits it
            // Changes the version in Android.mk to match
        }
    }
}