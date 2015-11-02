// Copyright 2015 Google Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Wrapper over the settings files created by the Google Cloud SDK. No data is cached, so
    /// it is possible to have race conditions between gcloud and PowerShell. This is by design.
    /// gcloud is the source of truth for data.
    /// </summary>
    public class CloudSdkSettings
    {
        /// <summary>Environment variable which contains the Application Data settings.</summary>
        private const string AppdataEnvironmentVariable = "APPDATA";

        /// <summary>GCloud configuration directory in Windows, relative to %APPDATA%.</summary>
        private const string CloudSDKConfigDirectoryWindows = "gcloud";

        public CloudSdkSettings() {}

        /// <summary> 
        /// Returns the file path to the Cloud SDK configuration file. Returns null on any sort of
        /// error.
        /// </summary>
        public string GetConfigurationFilePath()
        {
            string appDataFolder = Environment.GetEnvironmentVariable(AppdataEnvironmentVariable);
            if (appDataFolder == null || !Directory.Exists(appDataFolder))
            {
                return null;
            }

            string defaultConfigFile = Path.Combine(
                appDataFolder,
                CloudSDKConfigDirectoryWindows,
                "configurations/config_default");

            if (!File.Exists(defaultConfigFile))
            {
                return null;
            }
            return defaultConfigFile;
        }

        protected string GetSettingsValue(string settingName)
        {
            string configFile = GetConfigurationFilePath();
            if (configFile == null)
            {
                return null;
            }

            // Look through all key/value pairs for the specific setting.
            string[] fileLines = File.ReadAllLines(configFile);
            string linePrefix = settingName + " = ";
            foreach (string fileLine in fileLines)
            {
                if (fileLine.StartsWith(linePrefix))
                {
                    return fileLine.Replace(linePrefix, "");
                }
            }

            return null;
        }

        /// <summary>Returns the default project for the Google Cloud SDK.</summary>
        public string GetDefaultProject()
        {
            return GetSettingsValue("project");
        }
    }
}
