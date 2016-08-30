// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
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
        protected static BaseClientService.Initializer GetBaseClientServiceInitializer()
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
        /// Returns the fully-qualified file path, properly relative file paths (taking the current Powershell
        /// environemnt into account.)
        ///
        /// This method eliminates a class of bug where cmdlets do not support relative file paths. Because
        /// Path.GetFile only handles file paths relative to the current (process) directory, which is not going to be
        /// correct if the user has changed the current directory within the PowerShell session.
        ///
        /// You should *always* call this instead of Path.GetFullPath inside of cmdlets.
        /// </summary>
        protected string GetFullPath(string filePath)
        {
            // If the path is already fully-qualified, go with that.
            if (Path.IsPathRooted(filePath))
            {
                return filePath;
            }

            // For relative files, qualify them based on where the PowerShell session is.
            string currentDirectory = SessionState.Path.CurrentLocation.Path;
            return Path.Combine(currentDirectory, filePath);
        }

        /// <summary>
        /// Sets properties and fields decordated with ConfigPropertyNameAttribute to their defaults, if necessary.
        /// </summary>
        // TODO(jimwp): Add new function called by this to replace capability in childeren.
        protected sealed override void BeginProcessing()
        {
            UpdateConfigPropertyNameAttribute();
        }

        /// <summary>
        /// Updates the properties of the cmdlet that are marked with a ConfigPropertyName attribute, are an
        /// active PowerShell parameter for the current parameter set, and do not yet have a value.
        /// </summary>
        protected void UpdateConfigPropertyNameAttribute()
        {
            foreach (PropertyInfo property in GetType().GetProperties())
            {
                ConfigPropertyNameAttribute configPropertyName =
                    (ConfigPropertyNameAttribute)Attribute.GetCustomAttribute(
                        property, typeof(ConfigPropertyNameAttribute));
                if (configPropertyName != null && IsActiveParameter(property))
                {
                    configPropertyName.SetConfigDefault(property, this);
                }
            }

            foreach (FieldInfo field in GetType().GetFields())
            {
                ConfigPropertyNameAttribute configPropertyName =
                    (ConfigPropertyNameAttribute)Attribute.GetCustomAttribute(
                        field, typeof(ConfigPropertyNameAttribute));
                if (configPropertyName != null && IsActiveParameter(field))
                {
                    configPropertyName.SetConfigDefault(field, this);
                }
            }
        }

        /// <summary>
        /// Checks if the member is a powershell parameter that applys to the currently active parameter set.
        /// </summary>
        /// <param name="member">
        /// The member of the cmdlet to check.
        /// </param>
        /// <returns>
        /// True if the member is a powershell parameter of the current parameter set, false otherwise.
        /// </returns>
        private bool IsActiveParameter(MemberInfo member)
        {
            var parameterAttributes = Attribute.GetCustomAttributes(member, typeof(ParameterAttribute))
                .OfType<ParameterAttribute>()
                .Where(pa => string.IsNullOrEmpty(pa.ParameterSetName) ||
                        pa.ParameterSetName.Equals("__AllParameterSets") ||
                        pa.ParameterSetName.Equals(ParameterSetName)
                 );
            return parameterAttributes.Any();
        }

        /// <summary>
        /// Provides a one-time, post-processing functionality for the cmdlet.
        /// </summary>
        // TODO(jimwp): Seal this and replace with new function for childern to override.
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

        /// <summary>
        /// The exeption to be thrown when the parameter set is invalid. Should only be called if the code is
        /// not properly handling all parameter sets.
        /// </summary>
        protected PSInvalidOperationException UnknownParameterSetException =>
            new PSInvalidOperationException($"{ParameterSetName} is not a valid parameter set.");

        /// <summary>
        /// Library method to pull the name of a project from a uri.
        /// </summary>
        /// <param name="uri">
        /// The uri that includes the project.
        /// </param>
        /// <returns>
        /// The name of the project.
        /// </returns>
        public static string GetProjectNameFromUri(string uri)
        {
            return GetUriPart("projects", uri);
        }

        /// <summary>
        /// Library method to pull a resource name from a Rest uri.
        /// </summary>
        /// <param name="resourceType">
        /// The type of resource to get the name of (e.g. projects, zones, instances)
        /// </param>
        /// <param name="uri">
        /// The uri to pull the resource name from.
        /// </param>
        /// <returns>
        /// The name of the resource i.e. the section of the uri following the resource type.
        /// </returns>
        public static string GetUriPart(string resourceType, string uri)
        {
            Match match = Regex.Match(uri, $"{resourceType}/(?<value>[^/]*)");
            return match.Groups["value"].Value;
        }
    }
}
