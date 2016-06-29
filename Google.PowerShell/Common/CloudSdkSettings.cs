// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Diagnostics;
using System.IO;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Wrapper over the settings files created by the Google Cloud SDK. No data is cached, so
    /// it is possible to have race conditions between gcloud and PowerShell. This is by design.
    /// gcloud is the source of truth for data.
    /// </summary>
    public class CloudSdkSettings
    {
        public class CommonProperties
        {
            public const string Project = "project";
            public const string Zone = "zone";
            public const string Region = "region";
        }

        /// <summary>Environment variable which contains the Application Data settings.</summary>
        private const string AppdataEnvironmentVariable = "APPDATA";

        /// <summary>GCloud configuration directory in Windows, relative to %APPDATA%.</summary>
        private const string CloudSDKConfigDirectoryWindows = "gcloud";

        /// <summary>Name of the Cloud SDK file containing the name of the active config.</summary>
        private const string ActiveConfigFileName = "active_config";

        /// <summary>Folder name where configuration files are stored.</summary>
        private const string ConfigurationsFolderName = "configurations";

        /// <summary>Name of the file containing the anonymous client ID.</summary>
        private const string ClientIDFileName = ".metricsUUID";

        // Prevent instantiation. Should just be a static utility class.
        private CloudSdkSettings() { }

        /// <summary>
        /// Returns the name of the current configuration. See `gcloud config configurations` for more information.
        /// Returns null on any sort of error. For example, before gcloud runs for the first time no configuration
        /// file is set.
        /// </summary>
        public static string GetCurrentConfigurationName()
        {
            string appDataFolder = Environment.GetEnvironmentVariable(AppdataEnvironmentVariable);
            if (appDataFolder == null || !Directory.Exists(appDataFolder))
            {
                return null;
            }

            string activeconfigFilePath = Path.Combine(
                appDataFolder,
                CloudSDKConfigDirectoryWindows,
                ActiveConfigFileName);
            try
            {
                return File.ReadAllText(activeconfigFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(String.Format("Error reading Cloud SDK active configuration file: {0}", ex.Message));
                return null;
            }
        }

        /// <summary> 
        /// Returns the file path to the current Cloud SDK configuration set's property file. Returns null on any
        /// sort of error.
        /// </summary>
        public static string GetCurrentConfigurationFilePath()
        {
            string appDataFolder = Environment.GetEnvironmentVariable(AppdataEnvironmentVariable);
            if (appDataFolder == null || !Directory.Exists(appDataFolder))
            {
                return null;
            }

            string defaultConfigFile = Path.Combine(
                appDataFolder,
                CloudSDKConfigDirectoryWindows,
                ConfigurationsFolderName,
                String.Format("config_{0}", GetCurrentConfigurationName()));

            if (!File.Exists(defaultConfigFile))
            {
                return null;
            }
            return defaultConfigFile;
        }

        /// <summary>
        /// Returns the setting with the given name from the currently active gcloud configuration.
        /// </summary>
        public static string GetSettingsValue(string settingName)
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
            catch (Exception ex)
            {
                Debug.WriteLine("Error reading Cloud SDK configuration file: {0}", ex.Message);
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
        public static string GetDefaultProject()
        {
            return GetSettingsValue("project");
        }

        /// <summary>
        /// Returns whether or not the user has opted-into of telemetry reporting. Defaults to false (opted-out).
        /// </summary>
        public static bool GetOptIntoUsageReporting()
        {
            string rawValue = GetSettingsValue("disable_usage_reporting");
            bool value;
            if (Boolean.TryParse(rawValue, out value))
            {
                // Invert the value, because the value stores whether it is *disabled*.
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
        public static string GetAnoymousClientID()
        {
            string appDataFolder = Environment.GetEnvironmentVariable(AppdataEnvironmentVariable);
            if (appDataFolder == null || !Directory.Exists(appDataFolder))
            {
                return null;
            }

            string uuidFile = Path.Combine(
                appDataFolder,
                CloudSDKConfigDirectoryWindows,
                ClientIDFileName);

            if (!File.Exists(uuidFile))
            {
                return Guid.NewGuid().ToString();
            }
            return File.ReadAllText(uuidFile);
        }
    }
}
