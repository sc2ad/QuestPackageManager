using System;
using System.IO;
using System.Reflection;

namespace QPM.Data
{
    /// <summary>
    /// A type that holds the configuration for QPM.
    /// Loaded from QPM executable directory on startup.
    /// </summary>
    public class QPMConfig
    {
        public double DependencyTimeoutSeconds { get; set; } = 300;
        public string CachePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Assembly.GetExecutingAssembly().GetName().Name + "_Temp");
        public bool UseSymlinks { get; set; } = true;
    }
}