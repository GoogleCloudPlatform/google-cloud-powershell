using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    public class ActiveUserConfig
    {
        public ActiveUserToken UserToken { get; set; }

        public string SentinelFile { get; private set; }

        public string CachedFingerPrint { get; private set; }

        private JToken propertiesJson;

        private static ActiveUserConfig s_activeUserConfig;
        private static SemaphoreSlim s_lock = new SemaphoreSlim(1);

        public async static Task<ActiveUserConfig> GetActiveUserConfig(bool refreshConfig = false)
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
                s_activeUserConfig = new ActiveUserConfig(await GCloudWrapper.GetActiveConfig());
                return s_activeUserConfig;
            }
            finally
            {
                s_lock.Release();
            }
        }

        public async static Task<ActiveUserToken> GetActiveUserToken(bool refreshToken = false)
        {
            ActiveUserConfig userConfig = null;
            if (!refreshToken)
            {
                userConfig = await GetActiveUserConfig();
                if (!userConfig.UserToken.IsExpired)
                {
                    return userConfig.UserToken;
                }
            }
            userConfig = await GetActiveUserConfig(refreshConfig: true);
            return userConfig.UserToken;
        }

        private ActiveUserConfig(string activeConfigJson)
        {
            JToken parsedConfigJson = JObject.Parse(activeConfigJson);
            JToken sentinelJson = parsedConfigJson.SelectToken("sentinels.config_sentinel");
            if (sentinelJson == null)
            {
                throw new FileNotFoundException("Sentinel file for current active configuration could not be found.");
            }
            SentinelFile = sentinelJson.Value<string>();
            CachedFingerPrint = GetCurrentConfigurationFingerPrint();
            JToken parsedCredentialJson = parsedConfigJson.SelectToken("credential");
            UserToken = new ActiveUserToken(parsedCredentialJson, CachedFingerPrint);
            propertiesJson = parsedConfigJson.SelectToken("configuration.properties");
        }

        public string GetPropertiesValue(string key)
        {
            string result = null;
            if (TryGetPropertiesValue(propertiesJson, key, ref result))
            {
                return result;
            }
            return result;
        }

        public bool CheckTokenValidity(ActiveUserToken token)
        {
            return token.User == GetCurrentConfigurationFingerPrint();
        }

        private bool TryGetPropertiesValue(JToken propertiesJson, string key, ref string value)
        {
            if (propertiesJson.Type == JTokenType.Array)
            {
                foreach (JToken childToken in propertiesJson.Children())
                {
                    if (TryGetPropertiesValue(childToken, key, ref value))
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
                    if (TryGetPropertiesValue(childProperty.Value, key, ref value))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns fingerprint of the current configuration. If result will only be changed if there is
        /// either a change in the sentinel file (which is touched by gcloud any time the configuration is changed)
        /// or if there is a change in any CLOUDSDK_* environment variables.
        /// </summary>
        private string GetCurrentConfigurationFingerPrint()
        {
            DateTime timeStamp = File.GetLastWriteTime(SentinelFile);
            string fingerprint = timeStamp.ToString();
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
