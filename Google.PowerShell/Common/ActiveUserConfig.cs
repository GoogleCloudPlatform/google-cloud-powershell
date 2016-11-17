using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    public class ActiveUserConfig
    {
        public ActiveUserToken UserToken { get; set; }

        public string SentinelFile { get; private set; }

        public string CachedFingerPrint { get; private set; }

        private static ActiveUserConfig s_activeUserConfig;

        public static ActiveUserConfig GetActiveUserConfig()
        {
            // TODO: ADD LOCKING MECHANISM
            if (s_activeUserConfig == null)
            {
                s_activeUserConfig = new ActiveUserConfig(GCloudWrapper.GetActiveConfig().Result);
                return s_activeUserConfig;
            }
            // Otherwise, we check that the finger print has not changed.
            string newFingerPrint = s_activeUserConfig.CurrentConfigurationFingerPrint;
            if (s_activeUserConfig.CachedFingerPrint != newFingerPrint)
            {
                s_activeUserConfig.CachedFingerPrint = newFingerPrint;
            }
            return s_activeUserConfig;
        }

        private ActiveUserConfig(JToken activeConfigJson)
        {
            JToken sentinelJson = activeConfigJson.SelectToken("sentinels.config_sentinel");
            if (sentinelJson == null)
            {
                throw new FileNotFoundException("Sentinel file for current active configuration could not be found.");
            }
            SentinelFile = sentinelJson.Value<string>();
            CachedFingerPrint = CurrentConfigurationFingerPrint;
            JToken parsedCredentialJson = activeConfigJson.SelectToken("credential");
            UserToken = new ActiveUserToken(parsedCredentialJson, CachedFingerPrint);
        }

        /// <summary>
        /// Returns fingerprint of the current configuration. If result will only be changed if there is
        /// either a change in the sentinel file (which is touched by gcloud any time the configuration is changed)
        /// or if there is a change in any CLOUDSDK_* environment variables.
        /// </summary>
        private string CurrentConfigurationFingerPrint
        {
            get
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
}
