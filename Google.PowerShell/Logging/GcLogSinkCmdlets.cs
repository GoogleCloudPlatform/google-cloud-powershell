// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Logging.v2;
using Google.Apis.Logging.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Management.Automation;
using System.Net;
using System.Xml;

namespace Google.PowerShell.Logging
{
    /// <summary>
    /// <para type="synopsis">
    /// Retrieves Stackdriver Log Sinks.
    /// </para>
    /// <para type="description">
    /// Retrieves one or more Stackdriver Log Sinks.
    /// If -Sink is not used, the cmdlet will return all the sinks under the specified project
    /// (default project if -Project is not used). Otherwise, the cmdlet will return a list of sinks
    /// matching the sink names specified in -Sink and will raise an error for any sinks that cannot be found.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcLogSink</code>
    ///   <para>This command retrieves all sinks in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLogSink -Sink "sink1", "sink2" -Project "my-project"</code>
    ///   <para>
    ///   This command retrieves 2 sinks ("sink1" and "sink2") in the project "my-project".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/api/tasks/exporting-logs#about_sinks)">
    /// [Log Sinks]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcLogSink")]
    public class GetGcLogSinkCmdlet : GcLogCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for sinks in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the sinks to be retrieved.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string[] Sink { get; set; }

        protected override void ProcessRecord()
        {
            if (Sink != null && Sink.Length > 0)
            {
                foreach (string sinkName in Sink)
                {
                    string formattedSinkName = PrefixProjectToSinkName(sinkName, Project);
                    try
                    {
                        ProjectsResource.SinksResource.GetRequest getRequest = Service.Projects.Sinks.Get(formattedSinkName);
                        WriteObject(getRequest.Execute());
                    }
                    catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        WriteResourceMissingError(
                            exceptionMessage: $"Sink '{sinkName}' does not exist in project '{Project}'.",
                            errorId: "SinkNotFound",
                            targetObject: sinkName);
                    }
                }
            }
            else
            {
                ProjectsResource.SinksResource.ListRequest listRequest = Service.Projects.Sinks.List($"projects/{Project}");
                do
                {
                    ListSinksResponse response = listRequest.Execute();
                    if (response.Sinks != null)
                    {
                        WriteObject(response.Sinks, true);
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                while (!Stopping && listRequest.PageToken != null);
            }
        }
    }

    [Cmdlet(VerbsCommon.New, "GcLogSink")]
    public class NewGcLogSinkCmdlet : GcLogEntryCmdletWithLogFilter
    {
        private class ParameterSetNames
        {
            public const string BigQueryDataSetDestination = "BigQueryDataSetDestination";
            public const string GcsBucketDestination = "GcsBucketDestination";
            public const string PubSubTopicDestination = "PubSubTopicDestination";
        }

        /// <summary>
        /// Enum of version format for log entry.
        /// See https://cloud.google.com/logging/docs/api/reference/rest/v2/organizations.sinks#versionformat.
        /// </summary>
        public enum LogEntryVersionFormat
        {
            /// <summary>
            /// LogEntry version 2 format.
            /// </summary>
            V2,

            /// <summary>
            /// LogEntry version 1 format.
            /// </summary>
            V1
        }

        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = false)]
        public LogEntryVersionFormat? OutputVersionFormat { get; set; }

        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string SinkName { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.GcsBucketDestination)]
        [ValidateNotNullOrEmpty]
        public string GcsBucketDestination { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.BigQueryDataSetDestination)]
        [ValidateNotNullOrEmpty]
        public string BigQueryDataSetDestination { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.PubSubTopicDestination)]
        [ValidateNotNullOrEmpty]
        public string PubSubTopicDestination { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter NoUniqueWriterIdentity { get; set; }

        protected override void ProcessRecord()
        {
            LogSink logSink = new LogSink()
            {
                Name = SinkName
            };

            string permissionRequest = "";

            switch (ParameterSetName)
            {
                case ParameterSetNames.GcsBucketDestination:
                    logSink.Destination = $"storage.googleapis.com/{GcsBucketDestination}";
                    permissionRequest = $"'Owner' permission to the bucket '{GcsBucketDestination}'.";
                    break;
                case ParameterSetNames.BigQueryDataSetDestination:
                    logSink.Destination = $"bigquery.googleapis.com/projects/{Project}/datasets/{BigQueryDataSetDestination}";
                    permissionRequest = $"'Can edit' permission to the dataset '{BigQueryDataSetDestination}'.";
                    break;
                case ParameterSetNames.PubSubTopicDestination:
                    logSink.Destination = $"pubsub.googleapis.com/projects/{Project}/topics/{PubSubTopicDestination}";
                    permissionRequest = $"'Editor' permission in the project '{Project}'.";
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            string logName = PrefixProjectToLogName(LogName, Project);
            // The sink already has before and after filter so we do not have to supply it.
            logSink.Filter = ConstructLogFilterString(
                logName: logName,
                logSeverity: Severity,
                selectedType: SelectedResourceType,
                before: null,
                after: null,
                otherFilter: Filter);

            if (OutputVersionFormat.HasValue)
            {
                logSink.OutputVersionFormat = Enum.GetName(typeof(LogEntryVersionFormat), OutputVersionFormat.Value).ToUpper();
            }

            if (Before.HasValue)
            {
                logSink.EndTime = XmlConvert.ToString(Before.Value, XmlDateTimeSerializationMode.Local);
            }

            if (After.HasValue)
            {
                logSink.StartTime = XmlConvert.ToString(After.Value, XmlDateTimeSerializationMode.Local);
            }

            ProjectsResource.SinksResource.CreateRequest createRequest = Service.Projects.Sinks.Create(logSink, $"projects/{Project}");
            if (NoUniqueWriterIdentity.IsPresent)
            {
                createRequest.UniqueWriterIdentity = !NoUniqueWriterIdentity.ToBool();
            }
            else
            {
                createRequest.UniqueWriterIdentity = true;
            }

            try
            {
                LogSink createdSink = createRequest.Execute();
                WriteObject(createdSink);
                Host.UI.WriteLine($"Please remember to grant '{createdSink?.WriterIdentity}' {permissionRequest}");
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"Cannot create '{LogName}' in project '{Project}' because it already exists.",
                    errorId: "SubscriptionAlreadyExists",
                    targetObject: LogName);
            }
        }
    }
}
