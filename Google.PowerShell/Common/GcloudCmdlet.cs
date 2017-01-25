// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.CloudResourceManager.v1;
using Google.Apis.CloudResourceManager.v1.Data;
using Google.Apis.Services;
using Microsoft.PowerShell.Commands;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;

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

        /// <summary>
        /// The project that is used by the cmdlet. This value will be used in reporting usage.
        /// The logic is in the Dispose function. We will map this project id to a project number.
        /// If the derived class leaves it as null, we will fall back to the active project in
        /// the Cloud SDK. The project number will also be converted to hash before we use it
        /// in reporting usage.
        /// </summary>
        public virtual string Project { get; set; }

        public GCloudCmdlet()
        {
            if (CloudSdkSettings.GetOptIntoUsageReporting())
            {
                string clientID = CloudSdkSettings.GetAnonymousClientID();
                _telemetryReporter = new GoogleAnalyticsCmdletReporter(clientID, AnalyticsEventCategory.CmdletInvocation);
            }
            else
            {
                _telemetryReporter = new InMemoryCmdletResultReporter();
            }

            // Only set upon successful completion of EndProcessing.
            _cmdletInvocationSuccessful = false;
        }

        /// <summary>
        /// Returns an instance of the Google Client API initializer, using the active user's credentials.
        /// </summary>
        public static BaseClientService.Initializer GetBaseClientServiceInitializer()
        {
            return new BaseClientService.Initializer()
            {
                HttpClientInitializer = new AuthenticateWithSdkCredentialsExecutor(),
                ApplicationName = "google-cloud-powershell",
            };
        }

        /// <summary>
        /// Given a path, try to get the fully-qualified path and returns the PowerShell Provider
        /// that resolves the path. For example, if the path is C:\Test, this will return "C:\Test"
        /// with provider as FileSystem. If the path is gs:\my-bucket, this wil return "my-bucket"
        /// with provider as GoogleCloudStorage.
        /// </summary>
        protected Tuple<string, ProviderInfo> GetFullPath(string path)
        {
            // Try to resolve the path using PowerShell.
            try
            {
                ProviderInfo provider = null;
                string[] result = GetResolvedProviderPathFromPSPath(path, out provider).ToArray();

                // Only return the resolved path if there are no ambiguities.
                // If path contains wildcards, then it may resolved to more than 1 path.
                if (result?.Length == 1)
                {
                    return new Tuple<string, ProviderInfo>(result[0], provider);
                }
            }
            catch (ItemNotFoundException itemEx)
            {
                // This exception is thrown if the path does not exist.
                // For example, if we try to resolve .\Blah.text in folder C:\Test
                // and the file does not exist, this exception will be thrown.
                // However, we can still resolve the path with GetUnresolvedProviderPathFromPSPath to C:\Test\Blah.text
                ProviderInfo provider = null;
                PSDriveInfo psDrive = null;
                string unresolvedPath = SessionState.Path?.GetUnresolvedProviderPathFromPSPath(
                    itemEx.ItemName, out provider, out psDrive);
                return new Tuple<string, ProviderInfo>(unresolvedPath, provider);
            }

            return null;
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
        protected string GetFullFilePath(string filePath)
        {
            // If the path is already fully-qualified, go with that.
            if (Path.IsPathRooted(filePath))
            {
                return filePath;
            }

            Tuple<string, ProviderInfo> pathAndProviderInfo = null;

            try
            {
                pathAndProviderInfo = GetFullPath(filePath);
            }
            catch
            {
                return filePath;
            }

            if (pathAndProviderInfo == null)
            {
                return filePath;
            }

            string resolvedPath = pathAndProviderInfo.Item1;
            ProviderInfo providerInfo = pathAndProviderInfo.Item2;
            if (string.IsNullOrWhiteSpace(resolvedPath)
                || providerInfo == null
                || providerInfo.ImplementingType != typeof(FileSystemProvider))
            {
                return filePath;
            }

            return resolvedPath;
        }

        /// <summary>
        /// Converts a PowerShell Hashtable object to a dictionary.
        /// By default, @{"a" = "b"} in PowerShell is passed to cmdlets as HashTable so we will have to
        /// convert it to a dictionary if we want to use function that requires Dictionary.
        /// </summary>
        protected static Dictionary<K, V> ConvertToDictionary<K, V>(Hashtable hashTable)
        {
            return hashTable.Cast<DictionaryEntry>().ToDictionary(kvp => (K)kvp.Key, kvp => (V)kvp.Value);
        }

        /// <summary>
        /// Helper function to write an exception when we try to create a resource that already exists.
        /// </summary>
        protected void WriteResourceExistsError(string exceptionMessage, string errorId, object targetObject)
        {
            ErrorRecord errorRecord = new ErrorRecord(
                new ArgumentException(exceptionMessage),
                errorId,
                ErrorCategory.ResourceExists,
                targetObject);
            WriteError(errorRecord);
        }

        /// <summary>
        /// Helper function to write an exception when we try to access a resource that does not exist.
        /// </summary>
        protected void WriteResourceMissingError(string exceptionMessage, string errorId, object targetObject)
        {
            ErrorRecord errorRecord = new ErrorRecord(
                new ItemNotFoundException(exceptionMessage),
                errorId,
                ErrorCategory.ResourceUnavailable,
                targetObject);
            WriteError(errorRecord);
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
                    property.GetCustomAttribute<ConfigPropertyNameAttribute>();
                if (configPropertyName != null && IsActiveParameter(property))
                {
                    configPropertyName.SetConfigDefault(property, this);
                }
            }

            foreach (FieldInfo field in GetType().GetFields())
            {
                ConfigPropertyNameAttribute configPropertyName =
                    field.GetCustomAttribute<ConfigPropertyNameAttribute>();
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
            var parameterAttributes = member.GetCustomAttributes<ParameterAttribute>()
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
            foreach (var attrib in this.GetType().GetTypeInfo().GetCustomAttributes())
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

            string projectNumber = null;

            // Try to convert the project ID into project number.
            // Swallow the error if we fail to do so and proceed to reporting.
            try
            {
                if (string.IsNullOrWhiteSpace(Project))
                {
                    Project = CloudSdkSettings.GetDefaultProject();
                }

                var resourceService = new CloudResourceManagerService(GetBaseClientServiceInitializer());
                ProjectsResource.GetRequest getRequest = resourceService.Projects.Get(Project);
                Project project = getRequest.Execute();
                projectNumber = project.ProjectNumber.ToString();
            }
            catch { }

            if (_cmdletInvocationSuccessful)
            {
                _telemetryReporter.ReportSuccess(cmdletName, parameterSet, projectNumber);
            }
            else
            {
                // TODO(chrsmith): Is it possible to get ahold of any exceptions the
                // cmdlet threw? If so, use that to determine a more appropriate error code.
                // We report 1 instead of 0 so that the data can be see in Google Analytics.
                // (null vs. 0 is ambiguous in the UI.)
                _telemetryReporter.ReportFailure(cmdletName, parameterSet, Project, 1);
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
