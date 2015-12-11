// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Management.Automation;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using System.Threading.Tasks;
using System.Reflection;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Base commandlet for all Google Cloud cmdlets.
    /// </summary>
    public abstract class GCloudCmdlet : PSCmdlet, IDisposable
    {
        protected IReportCmdletResults _telemetryReporter;
        protected bool _cmdletInvocationSuccessful;

        public GCloudCmdlet()
        {
            _telemetryReporter = new FakeCmdletResultReporter();
            if (CloudSdkSettings.GetOptIntoUsageReporting())
            {
                string clientID = CloudSdkSettings.GetAnoymousClientID();
                _telemetryReporter = new GoogleAnalyticsCmdletReporter(clientID);
            }

            // Only set upon successful completion of EndProcessing.
            _cmdletInvocationSuccessful = false;
        }

        /// <summary>
        /// Returns an instance of the Google Client API initializer, using the machine's default credentials.
        /// </summary>
        protected BaseClientService.Initializer GetBaseClientServiceInitializer()
        {
            // TODO(chrsmith): How does the AppDefaultCredentials work with Cloud SDK profiles?
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

        /// <summary>
        /// Provides a one-time, post-processing functionality for the cmdlet.
        /// </summary>
        protected override void EndProcessing()
        {
            base.EndProcessing();
            // EndProcessing is not called if the cmdlet threw an exception or the user cancelled
            // the execution. We use IDispose.Dispose to perform the final telemetry reporting.
            _cmdletInvocationSuccessful = true;
        }

        /// <summary>
        /// Returns the name of a properly annotated cmdlet, e.g. Test-GcsBucket, otherwise null.
        /// </summary>
        protected string GetCmdletName()
        {
            foreach (var attrib in this.GetType().GetCustomAttributes())
            {
                if (attrib is CmdletAttribute)
                {
                    CmdletAttribute cmdletAttrib = attrib as CmdletAttribute;
                    return String.Format("{0}-{1}", cmdletAttrib.VerbName, cmdletAttrib.NounName);
                }
            }
            return null;
        }

        public void Dispose()
        {
            string cmdletName = GetCmdletName() ?? "unknown-cmdlet";
            string parameterSet = ParameterSetName;
            // "__AllParameterSets" isn't super-useful in reports.
            if (String.IsNullOrWhiteSpace(parameterSet)
                || ParameterSetName == ParameterAttribute.AllParameterSets)
            {
                parameterSet = "Default";
            }

            if (_cmdletInvocationSuccessful)
            {
                _telemetryReporter.ReportSuccess(cmdletName, parameterSet);
            }
            else
            {
                // TODO(chrsmith): Is it possible to get ahold of any exceptions the
                // cmdlet threw? If so, use that to determine a more appropriate error code.
                // We report 1 instead of 0 so that the data can be see in Google Analytics.
                // (null vs. 0 is ambiguous in the UI.)
                _telemetryReporter.ReportFailure(cmdletName, parameterSet, 1);
            }
        }
    }
}
