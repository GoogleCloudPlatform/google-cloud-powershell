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
        public override string Project { get; set; }

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
    /// Abstract class for cmdlet that create or update a log sink (both APIs have the same parameters).
    /// </summary>
    public abstract class CreateOrSetGcLogSinkCmdlet : GcLogEntryCmdletWithLogFilter
    {
        protected class ParameterSetNames
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
        /// The project to create the sink in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

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
        public virtual string GcsBucketDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the Google BigQuery dataset that the the sink will export the log entries to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.BigQueryDataSetDestination)]
        [ValidateNotNullOrEmpty]
        public virtual string BigQueryDataSetDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the Google PubSub topic that the the sink will export the log entries to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.PubSubTopicDestination)]
        [ValidateNotNullOrEmpty]
        public virtual string PubSubTopicDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// Determines the kind of IAM identity returned as writerIdentity in the new sink.
        /// If this value is not provided, then the value returned as writerIdentity is cloud-logs@google.com.
        /// Otherwise, it will be a unique service account.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public virtual SwitchParameter UniqueWriterIdentity { get; set; }

        /// <summary>
        /// Given a log sink, returns either a create or update request that will be used by the cmdlet.
        /// </summary>
        protected abstract LoggingBaseServiceRequest<LogSink> GetRequest(LogSink logsink);

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

            if (Before.HasValue)
            {
                WriteWarning("-Before parameter is deprecated for GcLogSink cmdlets.");
            }

            if (After.HasValue)
            {
                WriteWarning("-After parameter is deprecated for GcLogSink cmdlets.");
            }

            LoggingBaseServiceRequest<LogSink> request = GetRequest(logSink);

            try
            {
                LogSink createdSink = request.Execute();
                WriteObject(createdSink);
                // We want to let the user knows that they have to grant appropriate permission to the writer identity
                // so that the logs can be exported (otherwise, the export will fail).
                Host.UI.WriteLine($"Please remember to grant '{createdSink?.WriterIdentity}' {permissionRequest}");
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"Cannot create '{SinkName}' in project '{Project}' because it already exists.",
                    errorId: "SinkAlreadyExists",
                    targetObject: LogName);
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
    /// The identity of the writer of the logs will be cloud-logs@system.gserviceaccount.com by default
    /// if -UniqueWriterIdentity is not used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcLogSink -SinkName "my-sink" -GcsBucketDestination "my-bucket"</code>
    ///   <para>
    ///   This command creates a sink name "my-sink" that exports every log entry in the default project to the
    ///   Google Cloud Storage bucket "my-bucket". The identity of the writer of the logs will be cloud-logs@system.gserviceaccount.com.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcLogSink -SinkName "my-sink" -BigQueryDataSetDestination "my_dataset" -LogName "my-log" -Project "my-project"
    ///   </code>
    ///   <para>
    ///   This command creates a sink name "my-sink" that exports every log entry in the log "my-log" in the
    ///   project "my-project" to the Google Cloud BigQuery dataset "my_dataset" (also in the project "my-project").
    ///   The identity of the writer of the logs will be cloud-logs@system.gserviceaccount.com.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcLogSink -SinkName "my-sink" -PubSubTopicDestination "my_dataset" -Filter 'textPayload = "textPayload"' -UniqueWriterIdentity
    ///   </code>
    ///   <para>
    ///   This command creates a sink name "my-sink" that exports every log entry that matches the provided filter to
    ///   the Google Cloud PubSub topic "my-topic". The identity of the writer of the logs will be a unique service account.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/basic-concepts#sinks)">
    /// [Log Sinks]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/export/using_exported_logs)">
    /// [Exporting Logs]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcLogSink", DefaultParameterSetName = ParameterSetNames.GcsBucketDestination)]
    public class NewGcLogSinkCmdlet : CreateOrSetGcLogSinkCmdlet
    {
        protected override LoggingBaseServiceRequest<LogSink> GetRequest(LogSink logSink)
        {
            ProjectsResource.SinksResource.CreateRequest createRequest = Service.Projects.Sinks.Create(logSink, $"projects/{Project}");
            if (UniqueWriterIdentity.IsPresent)
            {
                createRequest.UniqueWriterIdentity = UniqueWriterIdentity.ToBool();
            }
            return createRequest;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates properties of a log sink. If the sink does not exist, the cmdlet will create the sink.
    /// </para>
    /// <para type="description">
    /// Updates properties of a log sink. If the sink does not exist, the cmdlet will create the sink. The cmdlet
    /// will use the default project if -Project is not used.
    /// </para>
    /// <para type="description">
    /// There are 3 possible destinations for the sink: Google Cloud Storage bucket, Google BigQuery dataset
    /// and Google Cloud PubSub topic. The destinations must be created and given appropriate permissions for
    /// log exporting (see https://cloud.google.com/logging/docs/export/configure_export_v2#destination_authorization)
    /// The cmdlet will not create the destinations.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Set-GcLogSink -SinkName "my-sink" -GcsBucketDestination "my-bucket"</code>
    ///   <para>
    ///   This command changes the destination of the sink name "my-sink" in the default project to "my-bucket".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GcLogSink -SinkName "my-sink" -BigQueryDataSetDestination "my_dataset" -LogName "my-log" -Project "my-project"
    ///   </code>
    ///   <para>
    ///   This command changes the destination of the sink name "my-sink" in the project "my-project" to the big query dataset "my_dataset".
    ///   The sink will now only export log from "my-log".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GcLogSink -SinkName "my-sink" -Filter 'textPayload = "textPayload"' -UniqueWriterIdentity
    ///   </code>
    ///   <para>
    ///   This command updates the filter of the log sink "my-sink" to 'textPayload = "textPayload"' and updates the
    ///   writer identity of the log sink to a unique service account.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/basic-concepts#sinks)">
    /// [Log Sinks]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/export/using_exported_logs)">
    /// [Exporting Logs]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcLogSink", DefaultParameterSetName = "__AllParameterSets")]
    public class SetGcLogSinkCmdlet : CreateOrSetGcLogSinkCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the Google Cloud Storage bucket that the sink will export the log entries to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.GcsBucketDestination)]
        [ValidateNotNullOrEmpty]
        public override string GcsBucketDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the Google BigQuery dataset that the the sink will export the log entries to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.BigQueryDataSetDestination)]
        [ValidateNotNullOrEmpty]
        public override string BigQueryDataSetDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the Google PubSub topic that the the sink will export the log entries to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.PubSubTopicDestination)]
        [ValidateNotNullOrEmpty]
        public override string PubSubTopicDestination { get; set; }

        /// <summary>
        /// <para type="description">
        /// Determines the kind of IAM identity returned as writerIdentity in the sink.
        /// If previously, the sink's writer identity is cloud-logs service account, then the writer identity of the sink will now
        /// be changed to a unique service account. If the sink already has a unique writer identity, then this has no effect.
        /// Note that if the old sink has a unique writer identity, it will be an error to set this to false.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public override SwitchParameter UniqueWriterIdentity { get; set; }

        protected override LoggingBaseServiceRequest<LogSink> GetRequest(LogSink logSink)
        {
            string formattedSinkName = PrefixProjectToSinkName(SinkName, Project);

            bool destinationNotSpecified = GcsBucketDestination == null
                && BigQueryDataSetDestination == null && PubSubTopicDestination == null;

            // First checks whether the sink exists or not.
            try
            {
                ProjectsResource.SinksResource.GetRequest getRequest = Service.Projects.Sinks.Get(formattedSinkName);
                LogSink existingSink = getRequest.Execute();

                // If destinations are not given, we still have to set the destination to the existing log sink destination.
                // Otherwise, the API will throw error.
                if (destinationNotSpecified)
                {
                    logSink.Destination = existingSink.Destination;
                }
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                // If log sink does not exist to begin with, then we at least need a destination for the sink.
                // So simply throws a terminating error if we don't have that.
                if (destinationNotSpecified)
                {
                    // Here we throw terminating error because the cmdlet cannot proceed without a valid sink.
                    string exceptionMessage = $"Sink '{SinkName}' does not exist in project '{Project}'." +
                                          "Please use New-GcLogSink cmdlet to create it (Set-GcLogSink can only create a sink if you supply a destination).";
                    ErrorRecord errorRecord = new ErrorRecord(
                        new ArgumentException(exceptionMessage),
                        "SinkNotFound",
                        ErrorCategory.ResourceUnavailable,
                        SinkName);
                    ThrowTerminatingError(errorRecord);
                }

                // Otherwise, returns a create request to create the log sink.
                ProjectsResource.SinksResource.CreateRequest createRequest = Service.Projects.Sinks.Create(logSink, $"projects/{Project}");
                if (UniqueWriterIdentity.IsPresent)
                {
                    createRequest.UniqueWriterIdentity = UniqueWriterIdentity.ToBool();
                }
                return createRequest;
            }

            ProjectsResource.SinksResource.UpdateRequest updateRequest = Service.Projects.Sinks.Update(logSink, formattedSinkName);
            if (UniqueWriterIdentity.IsPresent)
            {
                updateRequest.UniqueWriterIdentity = UniqueWriterIdentity.ToBool();
            }
            return updateRequest;
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes one or more log sinks from a project.
    /// </para>
    /// <para type="description">
    /// Removes one or more log sinks from a project based on the name of the log.
    /// If -Project is not specified, the default project will be used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcLogSink -SinkName "my-sink"</code>
    ///   <para>This command removes "my-sink" from the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcLogSink -SinkName "my-sink", "my-sink2" -Project "my-project"</code>
    ///   <para>This command removes "my-sink" and "my-sink2" from project "my-project".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/export/using_exported_logs#sink-service-destination)">
    /// [Log Sinks]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcLogSink", SupportsShouldProcess = true)]
    public class RemoveGcLogSinkCmdlet : GcLogCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for log sinks in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the sinks to be removed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [ArrayPropertyTransform(typeof(LogSink), nameof(LogSink.Name))]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string[] SinkName { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string sink in SinkName)
            {
                string formattedSinkName = PrefixProjectToSinkName(sink, Project);
                try
                {
                    if (ShouldProcess(formattedSinkName, "Remove Sink"))
                    {
                        ProjectsResource.SinksResource.DeleteRequest request = Service.Projects.Sinks.Delete(formattedSinkName);
                        request.Execute();
                    }
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        exceptionMessage: $"Sink '{sink}' does not exist in project '{Project}'.",
                        errorId: "SinkNotFound",
                        targetObject: sink);
                }
            }
        }
    }
}
