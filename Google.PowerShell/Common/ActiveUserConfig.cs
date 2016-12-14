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
    /// </summary>
    public class ActiveUserConfig
    {
        /// <summary>
        /// The token that belongs to this config.
        /// </summary>
        public TokenResponse UserToken { get; set; }

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
        private JToken propertiesJson;

        /// <summary>
        /// Cache of the current active user config.
        /// </summary>
        private static ActiveUserConfig s_activeUserConfig;

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
            if (s_activeUserConfig != null && !refreshConfig)
            {
                // Check that finger print has not changed.
                string newFingerPrint = s_activeUserConfig.GetCurrentConfigurationFingerPrint();
                if (s_activeUserConfig.CachedFingerPrint == newFingerPrint)
                {
                    return s_activeUserConfig;
                }
            }

            // Otherwise, we have to create a new active config.
            await s_lock.WaitAsync();
            try
            {
                string activeConfigJson = await GCloudWrapper.GetActiveConfig();
                cancellationToken.ThrowIfCancellationRequested();
                s_activeUserConfig = new ActiveUserConfig(activeConfigJson);
                return s_activeUserConfig;
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
        private ActiveUserConfig(string activeConfigJson)
        {
            JToken parsedConfigJson = JObject.Parse(activeConfigJson);

            // Parse the sentinels section to get the sentinel file.
            JToken sentinelJson = parsedConfigJson.SelectToken("sentinels.config_sentinel");
            if (sentinelJson == null)
            {
                throw new FileNotFoundException("Sentinel file for current active configuration could not be found.");
            }
            SentinelFile = sentinelJson.Value<string>();
            CachedFingerPrint = GetCurrentConfigurationFingerPrint();

            // Parse the credential section to get the access token.
            JToken parsedCredentialJson = parsedConfigJson.SelectToken("credential");
            UserToken = new TokenResponse(parsedCredentialJson);

            // Parse the properties section to get properties of the current configuration.
            propertiesJson = parsedConfigJson.SelectToken("configuration.properties");
        }

        /// <summary>
        /// Returns a property of the configuration based on the given key.
        /// </summary>
        public string GetPropertyValue(string key)
        {
            string result = null;
            if (TryGetPropertyValue(propertiesJson, key, ref result))
            {
                return result;
            }
            return result;
        }

        /// <summary>
        /// Search the JToken and its children recursively for a key that matches the given key.
        /// If such a key is found, set the value to the ref variable value and returns true.
        /// Otherwise, returns false.
        /// </summary>
        private bool TryGetPropertyValue(JToken propertiesJson, string key, ref string value)
        {
            if (propertiesJson.Type == JTokenType.Array)
            {
                foreach (JToken childToken in propertiesJson.Children())
                {
                    if (TryGetPropertyValue(childToken, key, ref value))
                    {
                        return true;
                    }
                }
            }
            else if (propertiesJson.Type == JTokenType.Object)
            {
                // We iterate through each child token of type JProperty, if the child token has
                // the same name as key, then we are done. Otherwise, recursively call this method
                // with the child token to continue the search.
                foreach (JProperty childProperty in propertiesJson.Children<JProperty>())
                {
                    if (string.Equals(childProperty.Name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = childProperty.Value?.Value<string>();
                        return true;
                    }
                    if (TryGetPropertyValue(childProperty.Value, key, ref value))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns fingerprint of the current configuration. The result will only be changed if there is
        /// either a change in the sentinel file (which is touched by gcloud any time the configuration is changed)
        /// or if there is a change in any CLOUDSDK_* environment variables.
        /// </summary>
        private string GetCurrentConfigurationFingerPrint()
        {
            // Gets the last time sentinel file was touched.
            DateTime timeStamp = File.GetLastWriteTime(SentinelFile);
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
