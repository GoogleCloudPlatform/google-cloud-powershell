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

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new log sink.
    /// </para>
    /// <para type="description">
    /// Creates a new log sink to export log entries. The sink will be created in the default project if -Project is not used.
    /// Will raise an error if the sink already exists.
    /// There are 3 possible destinations for the sink: Google Cloud Storage bucket, Google BigQuery dataset
    /// and Google Cloud PubSub topic. The destinations must be created and given appropriate permissions for
    /// log exporting (see https://cloud.google.com/logging/docs/export/configure_export_v2#destination_authorization)
    /// The cmdlet will not create the destinations.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcLogSink -SinkName "my-sink" -GcsBucketDestination "my-bucket"</code>
    ///   <para>
    ///   This command creates a sink name "my-sink" that exports every log entry in the default project to the
    ///   Google Cloud Storage bucket "my-bucket".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcLogSink -SinkName "my-sink" -BigQueryDataSetDestination "my_dataset" -LogName "my-log" -Project "my-project"
    ///   </code>
    ///   <para>
    ///   This command creates a sink name "my-sink" that exports every log entry in the log "my-log" in the
    ///   project "my-project" to the Google Cloud BigQuery dataset "my_dataset" (also in the project "my-project").
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcLogSink -SinkName "my-sink" -PubSubTopicDestination "my_dataset" -ResourceType gce_instance -After [DateTime]::Now().AddDays(1)
    ///   </code>
    ///   <para>
    ///   This command creates a sink name "my-sink" that exports every log entry of the resource type gce_instance that is created the next day
    ///   onwards to the Google Cloud PubSub topic "my-topic".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcLogSink -SinkName "my-sink" -PubSubTopicDestination "my_dataset" -Filter 'textPayload = "textPayload"' -NoUniqueWriterIdentity
    ///   </code>
    ///   <para>
    ///   This command creates a sink name "my-sink" that exports every log entry that matches the provided filter to
    ///   the Google Cloud PubSub topic "my-topic". The identity of the writer of the logs will be cloud-logs@google.com instead of
    ///   a unique service account created for this sink.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/basic-concepts#sinks)">
    /// [Log Sinks]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/export/using_exported_logs)">
    /// [Exporting Logs]
    /// </para>
    /// </summary>
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

        /// <summary>
        /// <para type="description">
        /// The log entry format to use for this sink's exported log entries. The v2 format is used by default.
        /// The v1 format is deprecated and should be used only as part of a migration effort to v2.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public LogEntryVersionFormat? OutputVersionFormat { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the sink to be created. This name must be unique within the project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string SinkName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the Google Cloud Storage bucket that the sink will export the log entries to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.GcsBucketDestination)]
        [ValidateNotNullOrEmpty]
        public string GcsBucketDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the Google BigQuery dataset that the the sink will export the log entries to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.BigQueryDataSetDestination)]
        [ValidateNotNullOrEmpty]
        public string BigQueryDataSetDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the Google PubSub topic that the the sink will export the log entries to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.PubSubTopicDestination)]
        [ValidateNotNullOrEmpty]
        public string PubSubTopicDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// Determines the kind of IAM identity returned as writerIdentity in the new sink.
        /// If this value is provided, then the value returned as writerIdentity is cloud-logs@google.com.
        /// Otherwise, it will be a unique service account.
        /// </para>
        /// </summary>
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
            createRequest.UniqueWriterIdentity = NoUniqueWriterIdentity.IsPresent ? !NoUniqueWriterIdentity.ToBool() : true;

            try
            {
                LogSink createdSink = createRequest.Execute();
                WriteObject(createdSink);
                // We want to let the user knows that they have to grant appropriate permission to the writer identity
                // so that the logs can be exported (otherwise, the export will fail).
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
