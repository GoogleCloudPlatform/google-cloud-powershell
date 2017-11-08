// Copyright 2015-2016 Google Inc. All Rights Reserved.
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
using System.Threading;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// This class shell executes "gcloud {command} --format=json",
    /// to allow delegation to Cloud SDK implementation. Used for things like
    /// credential management.
    /// </summary>
    public static class GCloudWrapper
    {
        /// <summary>
        /// Gets the current active gcloud config by calling config-helper.
        /// Every call will also create a new access token in the string returned.
        /// </summary>
        /// <returns></returns>
        public static async Task<string> GetActiveConfig()
        {
            return await GetGCloudCommandOutput("config config-helper --force-auth-refresh");
        }

        /// <summary>
        /// Execute cmd.exe /c "gcloud {command} --format=json" and returns
        /// the standard output if the command returns exit code 0.
        /// This will not pop up any new windows.
        /// The environment parameter is used to set environment variable for the execution.
        /// </summary>
        private static async Task<string> GetGCloudCommandOutput(string command, IDictionary<string, string> environment = null)
        {
            var actualCommand = CloudSdkSettings.IsWindows ? $"gcloud {command} --format=json" : $"{command} --format=json";
            ProcessOutput processOutput = CloudSdkSettings.IsWindows ?
                await ProcessUtils.GetCommandOutput("cmd.exe", $"/c \"{actualCommand}\"", environment) :
                await ProcessUtils.GetCommandOutput("gcloud", $"{actualCommand}", environment);

            if (processOutput.Succeeded)
            {
                return processOutput.StandardOutput;
            }

            if (!string.IsNullOrWhiteSpace(processOutput.StandardError))
            {
                throw new InvalidOperationException($"Command {actualCommand} failed with error: {processOutput.StandardError}");
            }

            throw new InvalidOperationException($"Command {actualCommand} failed.");
        }
    }
}
