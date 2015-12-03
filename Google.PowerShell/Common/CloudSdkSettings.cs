// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Google.PowerShell.Common
{
    // TODO(chrsmith): Confirm settings can be read based even if the gcloud install
    // is per-user instead of per-system.
    // TODO(chrsmith): What if the user chooses to install to a non-default path?

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

        // TODO(chrsmith): Put in a new reg key specifically for this purpose. The "uninstall"
        // reg key isn't ideal.
        /// <summary>Registry key to get the installed path of the Cloud SDK.</summary>
        private const string CloudSDKInstallPathRegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Google Cloud SDK";
        private const string CloudSDKInstallPathRegKeyName = "InstallLocation";

        public CloudSdkSettings() { }

        /// <summary>
        /// Returns the installation location of the Cloud SDK. Returns null if not found.
        /// </summary>
        /// <returns></returns>
        public string GetCloudSdkInstallPath()
        {
            // Check both HKCU and HKLM, since the location depends on if gcloud was
            // installed as an admin or not.
            RegistryKey hkcuKey = Registry.CurrentUser.OpenSubKey(CloudSDKInstallPathRegKey);
            string hkcuValue = hkcuKey.GetValue(CloudSDKInstallPathRegKeyName, "") as string;
            if (!String.IsNullOrEmpty(hkcuValue))
            {
                return hkcuValue.Replace("\"", "");
            }

            RegistryKey hklmKey = Registry.LocalMachine.OpenSubKey(CloudSDKInstallPathRegKey);
            string hklmValue = hklmKey.GetValue(CloudSDKInstallPathRegKeyName, "") as string;
            if (!String.IsNullOrEmpty(hklmValue))
            {
                return hklmValue.Replace("\"", "");
            }

            return null;
        }

        /// <summary>
        /// Returns the file path to the Cloud SDK configuration for its Python code. (Not to
        /// be confused with the more general configuration file.)
        /// </summary>
        /// <returns></returns>
        public string GetPythonConfigurationFilePath()
        {
            string installFolder = GetCloudSdkInstallPath();
            if (installFolder == null)
            {
                return null;
            }

            string configFile = Path.Combine(
                installFolder,
                @"google-cloud-sdk\properties");

            if (!File.Exists(configFile))
            {
                return null;
            }
            return configFile;
        }

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

        protected string GetSettingsValue(string configFilePath, string settingName)
        {
            if (configFilePath == null || !File.Exists(configFilePath))
            {
                return null;
            }

            // Look through all key/value pairs for the specific setting.
            string linePrefix = settingName + " = ";
            foreach (string fileLine in File.ReadAllLines(configFilePath))
            {
                if (fileLine.StartsWith(linePrefix))
                {
                    return fileLine.Replace(linePrefix, "");
                }
            }

            return null;
        }

        // TODO(chrsmith): Deal with Cloud SDK named configurations.

        /// <summary>Returns the default project for the Google Cloud SDK.</summary>
        public string GetDefaultProject()
        {
            return GetSettingsValue(GetConfigurationFilePath(), "project");
        }

        /// <summary>
        /// Returns if the user has opted-in to reporting anonymous usage metrics. Returns
        /// false if there was any problem reading the configuration data.
        /// </summary>
        public bool GetOptIntoReportingSetting()
        {
            string rawValue = GetSettingsValue(GetPythonConfigurationFilePath(), "disable_usage_reporting");
            if (rawValue == null)
            {
                return false;
            }

            // Return !value because the setting is *disable*_usage_reporting.
            bool value = false;
            return bool.TryParse(rawValue, out value) ? !value : false;
        }

        /// <summary>
        /// Client ID refers to the random UUID generated to group telemetry reporting.
        ///
        /// The file is generated on-demand by the Python code. Returns a new UUID if
        /// the file isn't found. (Meaning we will generate new UUIDs until the Python
        /// code gets executed.)
        /// </summary>
        public string GetAnoymousClientID()
        {
            string appDataFolder = Environment.GetEnvironmentVariable(AppdataEnvironmentVariable);
            if (appDataFolder == null || !Directory.Exists(appDataFolder))
            {
                return null;
            }

            string uuidFile = Path.Combine(
                appDataFolder,
                CloudSDKConfigDirectoryWindows,
                ".metricsUUID");

            if (!File.Exists(uuidFile))
            {
                return Guid.NewGuid().ToString();
            }
            return File.ReadAllText(uuidFile);
        }
    }
}
