// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

        /// <summary>
        /// Environment variable which points to the location of the current configuration file.
        /// This overrides the user configuration file as well as the global installation properties file.
        /// </summary>
        private const string CloudSdkConfigVariable = "CLOUDSDK_CONFIG";

        /// <summary>
        /// Environment variable which stores the current active config.
        /// This overrides value found in active_config file if present.
        /// </summary>
        private const string CloudSdkActiveConfigNameVariable = "CLOUDSDK_ACTIVE_CONFIG_NAME";

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

        /// <summary>
        /// Backing field for the InstallationPropertiesPath property.
        /// </summary>
        private static string s_installationPropertiesPath;

        private static JToken s_activeConfig;
        
        // Prevent instantiation. Should just be a static utility class.
        private CloudSdkSettings() { }

        /// <summary>
        /// Returns path to the properties file where GoogleCloud SDK is installed.
        /// </summary>
        public static string InstallationPropertiesPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(s_installationPropertiesPath))
                {
                    s_installationPropertiesPath = GCloudWrapper.GetInstallationPropertiesPath().Result;
                }
                return s_installationPropertiesPath;
            }
        }

        /// <summary>
        /// Returns the name of the current configuration. See `gcloud config configurations` for more information.
        /// Returns null on any sort of error. For example, before gcloud runs for the first time no configuration
        /// file is set.
        /// If CLOUDSDK_ACTIVE_CONFIG_NAME is set, we will use that as the active config.
        /// If not, we look into the active_config file to determine the active config.
        /// </summary>
        public static string GetCurrentConfigurationName()
        {
            string activeConfigName = Environment.GetEnvironmentVariable(CloudSdkActiveConfigNameVariable);

            if (!string.IsNullOrWhiteSpace(activeConfigName))
            {
                return activeConfigName;
            }

            string cloudConfigDir = GetCurrentConfigurationDirectory();

            string activeconfigFilePath = Path.Combine(
                cloudConfigDir,
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
        /// Returns the setting with the given name from the currently active gcloud configuration.
        /// We first look into the user config file. If we cannot find anything, we look
        /// into the global properties file.
        /// </summary>
        public static string GetSettingsValue(string settingName)
        {
            string userConfigFile = GetCurrentConfigurationFilePath();
            string userConfigSetting = GetSettingsValueFromFile(userConfigFile, settingName);

            if (string.IsNullOrWhiteSpace(userConfigSetting))
            {
                userConfigSetting = GetSettingsValueFromFile(InstallationPropertiesPath, settingName);
            }

            return userConfigSetting;
        }

        /// <summary>
        /// Retrieves the setting with the given name from a config file.
        /// </summary>
        private static string GetSettingsValueFromFile(string configFile, string settingName)
        {
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
            bool value = false;
            // If the disable_usage_reporting value is not set, fall back to the install default.
            // (false, meaning to report usage.)
            if (rawValue == null || Boolean.TryParse(rawValue, out value))
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
