// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

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

        public CloudSdkSettings() { }

        /// <summary>
        /// Returns the name of the current configuration. See `gcloud config configurations` for more information.
        /// Returns null on any sort of error. For example, before gcloud runs for the first time no configuration
        /// file is set.
        /// </summary>
        public string GetCurrentConfigurationName()
        {
            string appDataFolder = Environment.GetEnvironmentVariable(AppdataEnvironmentVariable);
            if (appDataFolder == null || !Directory.Exists(appDataFolder))
            {
                return null;
            }

            string activeconfigFilePath = Path.Combine(
                appDataFolder,
                CloudSDKConfigDirectoryWindows,
                "active_config");
            try
            {
                return File.ReadAllText(activeconfigFilePath);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary> 
        /// Returns the file path to the current Cloud SDK configuration set's property file. Returns null on any
        /// sort of error.
        /// </summary>
        public string GetCurrentConfigurationFilePath()
        {
            string appDataFolder = Environment.GetEnvironmentVariable(AppdataEnvironmentVariable);
            if (appDataFolder == null || !Directory.Exists(appDataFolder))
            {
                return null;
            }

            string defaultConfigFile = Path.Combine(
                appDataFolder,
                CloudSDKConfigDirectoryWindows,
                "configurations/config_" + GetCurrentConfigurationName());

            if (!File.Exists(defaultConfigFile))
            {
                return null;
            }
            return defaultConfigFile;
        }

        /// <summary>
        /// Returns the setting with the given name from the currently active gcloud configuration.
        /// </summary>
        protected string GetSettingsValue(string settingName)
        {
            string configFile = GetCurrentConfigurationFilePath();
            if (configFile == null)
            {
                return null;
            }

            string[] configLines = null;
            try
            {
                if (!File.Exists(configFile))
                {
                    return null;
                }
                configLines = File.ReadAllLines(configFile);
            }
            catch (Exception)
            {
                return null;
            }

            // Look through all key/value pairs for the specific setting.
            string linePrefix = settingName + " = ";
            foreach (string fileLine in configLines)
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

        /// <summary>
        /// Returns whether or not the user has opted-in to reporting telemetry data. Defaults to false (opted-out).
        /// </summary>
        public bool GetOptIntoReportingSetting()
        {
            string rawValue = GetSettingsValue("disable_usage_reporting");
            bool value;
            if (Boolean.TryParse(rawValue, out value))
            {
                // We invert the value because the setting is "disable" reporting.
                return !value;
            }
            else
            {
                return false;
            }
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
