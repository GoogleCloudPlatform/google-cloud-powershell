// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Class that represents the current active gcloud config.
    /// The active gcloud config is the config that is listed when running "gcloud config list".
    /// This active config can be changed if the user run commands like "gcloud config set account"
    /// or if certain CLOUDSDK_* environment variables are changed.
    /// </summary>
    public class ActiveUserConfig
    {
        /// <summary>
        /// The token that belongs to this config.
        /// </summary>
        public TokenResponse UserToken { get; private set; }

        /// <summary>
        /// This file will be updated whenever there is a change to the active config.
        /// However, this file will not be updated if there is a change in the environment variables (CLOUDSDK_*)
        /// </summary>
        public string SentinelFile { get; private set; }

        /// <summary>
        /// This string will be changed whenever there is a change to the active config,
        /// even if that change is caused by a change in the environment variables.
        /// </summary>
        public string CachedFingerPrint { get; private set; }

        /// <summary>
        /// The JSON that represents the properties of this config.
        /// </summary>
        public JToken PropertiesJson { get; private set; }

        /// <summary>
        /// Cache of the current active user config.
        /// </summary>
        internal static ActiveUserConfig ActiveConfig { private get; set; }

        /// <summary>
        /// Lock to help prevents race condition when modifying the cache.
        /// </summary>
        private static SemaphoreSlim s_lock = new SemaphoreSlim(1);

        /// <summary>
        /// Gets the current active config. This value will be cached for the next call.
        /// If refreshConfig is true, however, we will refresh the cache. Everytime the cache is refreshed,
        /// a new access token will be generated for the current active config (even if there is no change
        /// in the current active config).
        /// </summary>
        public async static Task<ActiveUserConfig> GetActiveUserConfig(
            CancellationToken cancellationToken = default(CancellationToken),
            bool refreshConfig = false)
        {
            if (ActiveConfig != null && !refreshConfig)
            {
                // Check that finger print has not changed.
                string newFingerPrint = ActiveConfig.GetCurrentConfigurationFingerPrint();
                if (ActiveConfig.CachedFingerPrint == newFingerPrint)
                {
                    return ActiveConfig;
                }
            }

            // Otherwise, we have to create a new active config.
            await s_lock.WaitAsync();
            try
            {
                string activeConfigJson = await GCloudWrapper.GetActiveConfig();
                cancellationToken.ThrowIfCancellationRequested();
                ActiveConfig = new ActiveUserConfig(activeConfigJson);
                return ActiveConfig;
            }
            finally
            {
                s_lock.Release();
            }
        }

        /// <summary>
        /// Gets the token that belongs to the current active config.
        /// This value will be normally be cached as the current active user config is normally cached.
        /// If refresh is true or if the current token already expired, however, we will refresh
        /// the active user config to get a new token.
        /// </summary>
        public async static Task<TokenResponse> GetActiveUserToken(CancellationToken cancellationToken, bool refresh = false)
        {
            ActiveUserConfig userConfig = null;
            if (!refresh)
            {
                userConfig = await GetActiveUserConfig(cancellationToken);
                if (!userConfig.UserToken.IsExpired)
                {
                    return userConfig.UserToken;
                }
            }
            userConfig = await GetActiveUserConfig(cancellationToken, refreshConfig: true);
            return userConfig.UserToken;
        }

        /// <summary>
        /// Creates an active user config by parsing a JSON.
        /// </summary>
        internal ActiveUserConfig(string activeConfigJson)
        {
            JToken parsedConfigJson = JObject.Parse(activeConfigJson);
            if (parsedConfigJson == null)
            {
                throw new ArgumentException($"Unable to parse active config '{activeConfigJson}'.");
            }

            // Parse the sentinels section to get the sentinel file.
            JToken sentinelJson = parsedConfigJson.SelectToken("sentinels.config_sentinel");
            if (sentinelJson == null)
            {
                throw new InvalidDataException("Config JSON does not contain a sentinel file.");
            }
            SentinelFile = sentinelJson.Value<string>();
            CachedFingerPrint = GetCurrentConfigurationFingerPrint();

            // Parse the credential section to get the access token.
            JToken parsedCredentialJson = parsedConfigJson.SelectToken("credential");
            if (parsedCredentialJson == null)
            {
                throw new InvalidDataException("Config JSON does not contain the current credential.");
            }
            UserToken = new TokenResponse(parsedCredentialJson);

            // Parse the properties section to get properties of the current configuration.
            PropertiesJson = parsedConfigJson.SelectToken("configuration.properties");
            if (PropertiesJson == null)
            {
                throw new InvalidDataException("Config JSON does not contain the current properties.");
            }
        }

        /// <summary>
        /// Returns a property of the configuration based on the given key.
        /// </summary>
        public async static Task<string> GetPropertyValue(string key)
        {
            string result = null;
            ActiveUserConfig activeConfig = await GetActiveUserConfig();
            if (activeConfig != null && activeConfig.PropertiesJson.TryGetPropertyValue(key, ref result))
            {
                return result;
            }
            return result;
        }

        /// <summary>
        /// Returns fingerprint of the current configuration. The result will only be changed if there is
        /// either a change in the sentinel file (which is touched by gcloud any time the configuration is changed)
        /// or if there is a change in any CLOUDSDK_* environment variables.
        /// </summary>
        private string GetCurrentConfigurationFingerPrint()
        {
            // Gets the last time sentinel file was touched. Note that if gcloud is used for the first time,
            // the sentinel file wouldn't exist (it will only get created if there is a change in the config).
            // On Linux, File.GetLastWriteTime will throw error if the file does not exist so we have to set it to MinValue.
            DateTime timeStamp = File.Exists(SentinelFile) ? File.GetLastWriteTime(SentinelFile) : DateTime.MinValue;
            string fingerprint = timeStamp.ToString();

            // Gets all available CLOUDSDK_* environment variables and their values.
            IDictionary environmentDict = Environment.GetEnvironmentVariables();
            IEnumerable<string> sortedKeys = environmentDict.Keys
                .Cast<string>()
                .Where(key => key.StartsWith("CLOUDSDK_"))
                .OrderBy(s => s);

            foreach (string key in sortedKeys)
            {
                fingerprint += $"{key}:{environmentDict[key]};";
            }

            return fingerprint;
        }
    }
}
