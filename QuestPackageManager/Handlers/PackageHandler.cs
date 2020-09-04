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

        public event Action<PackageHandler, Config, Uri>? OnConfigUrlChanged;

        public event Action<PackageHandler, Uri>? OnUrlChanged;

        public event Action<PackageHandler, Config, string>? OnConfigNameChanged;

        public event Action<PackageHandler, string>? OnNameChanged;

        public PackageHandler(IConfigProvider configProvider)
        {
            this.configProvider = configProvider;
        }

        public void CreatePackage(PackageInfo info)
        {
            if (info is null)
                throw new ArgumentNullException(Resources.Info);
            var conf = configProvider.GetConfig(true);
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotCreated);
            var shared = configProvider.GetSharedConfig();
            var tmp = conf.Info;
            conf.Info = info;
            if (shared != null && shared.Config != null)
                shared.Config.Info = info;
            // Call extra modification as necessary
            try
            {
                OnPackageConfigured?.Invoke(this, conf, info);
            }
            catch
            {
                conf.Info = tmp;
                if (shared != null && shared.Config != null)
                    shared.Config.Info = tmp;
                throw;
            }
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
            var shared = configProvider.GetSharedConfig();
            var tmp = conf.Info.Url;
            conf.Info.Url = url;
            if (shared != null && shared.Config != null && shared.Config.Info != null)
                shared.Config.Info.Url = url;
            try
            {
                OnConfigUrlChanged?.Invoke(this, conf, url);
            }
            catch
            {
                conf.Info.Url = tmp;
                if (shared != null && shared.Config != null && shared.Config.Info != null)
                    shared.Config.Info.Url = tmp;
                throw;
            }
            configProvider.Commit();
            OnUrlChanged?.Invoke(this, url);
        }

        public void ChangeName(string name)
        {
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotFound);
            if (conf.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            var shared = configProvider.GetSharedConfig();
            var tmp = conf.Info.Name;
            conf.Info.Name = name;
            if (shared != null && shared.Config != null && shared.Config.Info != null)
                shared.Config.Info.Name = name;
            try
            {
                OnConfigNameChanged?.Invoke(this, conf, name);
            }
            catch
            {
                conf.Info.Name = tmp;
                if (shared != null && shared.Config != null && shared.Config.Info != null)
                    shared.Config.Info.Name = tmp;
                throw;
            }
            configProvider.Commit();
            OnNameChanged?.Invoke(this, name);
        }

        public void ChangeId(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(Resources._id);
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotFound);
            if (conf.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            var shared = configProvider.GetSharedConfig();
            var tmp = conf.Info.Id;
            conf.Info.Id = id;
            if (shared != null && shared.Config != null && shared.Config.Info != null)
                shared.Config.Info.Id = id;
            // Call extra modification as necessary
            try
            {
                OnConfigIdChanged?.Invoke(this, conf, id);
            }
            catch
            {
                conf.Info.Id = tmp;
                if (shared != null && shared.Config != null && shared.Config.Info != null)
                    shared.Config.Info.Id = tmp;
                throw;
            }
            configProvider.Commit();
            // Perform extra modification
            OnIdChanged?.Invoke(this, id);
            // Changes the ID of the package.
            // Grabs the config, modifies the ID, commits it
            // Changes the ID in Android.mk to match
        }

        public void ChangeVersion(SemVer.Version newVersion)
        {
            if (newVersion is null)
                throw new ArgumentNullException(Resources._newVersion);
            var conf = configProvider.GetConfig();
            if (conf is null)
                throw new ConfigException(Resources.ConfigNotFound);
            if (conf.Info is null)
                throw new ConfigException(Resources.ConfigInfoIsNull);
            var shared = configProvider.GetSharedConfig();
            // Call extra modification to config as necessary
            var tmp = conf.Info.Version;
            conf.Info.Version = newVersion;
            if (shared != null && shared.Config != null && shared.Config.Info != null)
                shared.Config.Info.Version = newVersion;
            try
            {
                OnConfigVersionChanged?.Invoke(this, conf, newVersion);
            }
            catch
            {
                conf.Info.Version = tmp;
                if (shared != null && shared.Config != null && shared.Config.Info != null)
                    shared.Config.Info.Version = tmp;
                throw;
            }
            configProvider.Commit();
            // Perform extra modification
            OnVersionChanged?.Invoke(this, newVersion);
            // Changes the version of the package.
            // Grabs the config, modifies the version, commits it
            // Changes the version in Android.mk to match
        }
    }
}