// Copyright 2016 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// This class wraps the gcloud command and offers up some of its services.
    /// </summary>
    public static class GCloudWrapper
    {
        /// <summary>
        /// Returns the global installation properties path of GoogleCloud SDK.
        /// </summary>
        /// <returns></returns>
        public static async Task<string> GetInstallationPropertiesPath()
        {
            string gCloudInfoOutput = await GetGCloudCommandOutput("info");
            JToken gCloudInfoJson = JObject.Parse(gCloudInfoOutput);

            try
            {
                // SelectToken will throw NullReferenceException if the token cannot be found.
                gCloudInfoJson = gCloudInfoJson.SelectToken("config.paths.installation_properties_path");
                
                if (gCloudInfoJson.Type == JTokenType.String)
                {
                    return gCloudInfoJson.Value<string>();
                }
            }
            catch (NullReferenceException)
            {
                // Throw exception at the end.
            }

            throw new FileNotFoundException("Installation Properties file for Google Cloud SDK cannot be found.");
        }

        /// <summary>
        /// Returns the access token of the current active config.
        /// </summary>
        public static async Task<ActiveUserToken> GetAccessToken()
        {
            // We get the issued time before the command so we won't be too late
            // when it comes to token expiry.
            DateTime issuedTime = DateTime.Now;

            string accessToken = await GetGCloudCommandOutput("auth print-access-token");
            JToken tokenJson = JObject.Parse(accessToken);

            try
            {
                // SelectToken will throw NullReferenceException if the token cannot be found.
                tokenJson = tokenJson.SelectToken("token_response");

                ActiveUserToken token = tokenJson.ToObject<ActiveUserToken>();
                token.Issued = issuedTime;

                return token;
            }
            catch (NullReferenceException)
            {
                // Throw exception at the end.
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Throw exception at the end.
            }

            throw new InvalidDataException("Failed to get access token from gcloud auth print-access-token.");
        }

        private static async Task<string> GetGCloudCommandOutput(string command, IDictionary<string, string> environment = null)
        {
            var actualCommand = $"gcloud {command} --format=json";
            // This code depends on the fact that gcloud.cmd is a batch file.
            Debug.Write($"Executing gcloud command: {actualCommand}");
            ProcessOutput processOutput = await ProcessUtils.GetCommandOutput("cmd.exe", $"/c {actualCommand}", environment);
            if (processOutput.Succeeded)
            {
                return processOutput.StandardOutput;
            }

            if (!string.IsNullOrWhiteSpace(processOutput.StandardError))
            {
                throw new InvalidOperationException($"Command {actualCommand} failed with error: processOutput.StandardError");
            }

            throw new InvalidOperationException($"Command {actualCommand} failed.");
        }
    }
}