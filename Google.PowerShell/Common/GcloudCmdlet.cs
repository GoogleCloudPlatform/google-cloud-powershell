// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using System;
using System.Management.Automation;
using System.Reflection;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Base commandlet for all Google Cloud cmdlets.
    /// </summary>
    public abstract class GCloudCmdlet : PSCmdlet, IDisposable
    {
        protected IReportCmdletResults _telemetryReporter;
        protected bool _cmdletInvocationSuccessful;

        /// <summary>Placeholder for an unknown cmdlet name when reporting telemetry.</summary>
        private const string UnknownCmdletName = "unknown-cmdlet";

        public GCloudCmdlet()
        {
            if (CloudSdkSettings.GetOptIntoUsageReporting())
            {
                string clientID = CloudSdkSettings.GetAnoymousClientID();
                _telemetryReporter = new GoogleAnalyticsCmdletReporter(clientID);
            }
            else
            {
                _telemetryReporter = new InMemoryCmdletResultReporter();
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
            try
            {
                getCredsTask.Wait();
            }
            catch (AggregateException ae)
            {
                // Unpackage the AggregateException to aid debugging. See:
                // https://github.com/google/google-api-dotnet-client/issues/652
                if (ae.InnerExceptions.Count == 1)
                {
                    throw ae.InnerException;
                }
                throw;
            }

            return new BaseClientService.Initializer()
            {
                HttpClientInitializer = getCredsTask.Result,
                ApplicationName = "Google Cloud PowerShell",
            };
        }

        /// <summary>
        /// Sets Properties and fields decordated with ConfigDefaultAttributes to their defaults, if necessary.
        /// </summary>
        // TODO(jimwp): Add new function called by this to replace capability in childeren.
        protected sealed override void BeginProcessing()
        {
            foreach (PropertyInfo property in GetType().GetProperties())
            {
                ConfigDefaultAttribute configDefault = (ConfigDefaultAttribute)Attribute.GetCustomAttribute(
                        property, typeof(ConfigDefaultAttribute));
                if (configDefault != null && property.GetMethod.Invoke(this, null) == null)
                {
                    string settingsValue = CloudSdkSettings.GetSettingsValue(configDefault.Property);
                    if (string.IsNullOrEmpty(settingsValue))
                    {
                        throw new PSInvalidOperationException(
                            $"Parameter {property.Name} was not set and does not have a default value.");
                    }

                    property.SetMethod.Invoke(this, new object[] { settingsValue });
                }
            }

            foreach (FieldInfo field in GetType().GetFields())
            {
                ConfigDefaultAttribute configDefault = (ConfigDefaultAttribute)Attribute.GetCustomAttribute(
                        field, typeof(ConfigDefaultAttribute));
                if (configDefault != null && field.GetValue(this) == null)
                {
                    string settingsValue = CloudSdkSettings.GetSettingsValue(configDefault.Property);
                    if (string.IsNullOrEmpty(settingsValue))
                    {
                        throw new PSInvalidOperationException(
                            $"Parameter {field.Name} was not set and does not have a default value.");
                    }

                    field.SetValue(this, settingsValue);
                }
            }
        }

        /// <summary>
        /// Provides a one-time, post-processing functionality for the cmdlet.
        /// </summary>
        // TODO(jimwp): seal this and replace with new function for childern to override.
        protected override void EndProcessing()
        {
            // EndProcessing is not called if the cmdlet threw an exception or the user cancelled
            // the execution. We use IDispose.Dispose to perform the final telemetry reporting.
            _cmdletInvocationSuccessful = true;
        }

        /// <summary>
        /// Returns the name of a properly annotated cmdlet, e.g. Test-GcsBucket, otherwise UnknownCmdletName.
        /// </summary>
        protected string GetCmdletName()
        {
            foreach (var attrib in this.GetType().GetCustomAttributes())
            {
                if (attrib is CmdletAttribute)
                {
                    var cmdletAttrib = attrib as CmdletAttribute;
                    return String.Format("{0}-{1}", cmdletAttrib.VerbName, cmdletAttrib.NounName);
                }
            }
            return UnknownCmdletName;
        }

        public void Dispose()
        {
            string cmdletName = GetCmdletName();
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
