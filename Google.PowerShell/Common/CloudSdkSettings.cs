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
        
        // Prevent instantiation. Should just be a static utility class.
        private CloudSdkSettings() { }

        /// <summary>
        /// Returns the setting with the given name from the currently active gcloud configuration.
        /// </summary>
        public static string GetSettingsValue(string settingName)
        {
            ActiveUserConfig userConfig = ActiveUserConfig.GetActiveUserConfig().Result;
            return userConfig?.GetPropertyValue(settingName);
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
        public static string GetAnonymousClientID()
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
