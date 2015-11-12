// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Management.Automation;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Base commandlet for all Google Cloud cmdlets.
    /// </summary>
    public abstract class GCloudCmdlet : PSCmdlet
    {
        public GCloudCmdlet()
        {
            CloudSdk = new CloudSdkSettings();
        }

        public CloudSdkSettings CloudSdk { get; protected set; }

        /// <summary>
        /// Returns an instance of the Google Client API initializer, using the machine's default credentials.
        /// </summary>
        protected BaseClientService.Initializer GetBaseClientServiceInitializer()
        {
            // TODO(chrsmith): Support the notion of "profiles" and switching between them.
            // This should be built into the CloudSdkSettings class.
            Task<GoogleCredential> getCredsTask = GoogleCredential.GetApplicationDefaultAsync();
            getCredsTask.Wait();

            return new BaseClientService.Initializer()
            {
                HttpClientInitializer = getCredsTask.Result,
                ApplicationName = "Google Cloud PowerShell",
            };
        }

        /// <summary>
        /// Prompt the user that they are about to perform the given action. Returns true IFF
        /// the user confirms the action.
        /// 
        /// - Does not prompt if Force is set.
        /// - Always prompts if WhatIF is set.
        /// </summary>
        protected bool ConfirmAction(bool force, string resource, string action)
        {
            // Always prompt if -WhatIf is added.
            bool whatIfFlagValue = false;
            if (base.MyInvocation.BoundParameters.ContainsKey("WhatIf"))
            {
                SwitchParameter whatIfFlag = (SwitchParameter)base.MyInvocation.BoundParameters["WhatIf"];
                whatIfFlagValue = whatIfFlag.ToBool();
            }
            if (whatIfFlagValue)
            {
                return ShouldProcess(resource, action);
            }

            return force || ShouldProcess(resource, action);
        }
    }
}
