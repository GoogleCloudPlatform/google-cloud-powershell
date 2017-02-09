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
    /// Retrieves StackDriver Log Metrics.
    /// </para>
    /// <para type="description">
    /// Retrieves one or more StackDriver Log Metrics.
    /// If -MetricName is not used, the cmdlet will return all the log metrics under the specified project
    /// (default project if -Project is not used). Otherwise, the cmdlet will return a list of metrics
    /// matching the names specified in -MetricName and will raise an error for any metrics that cannot be found.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcLogMetric</code>
    ///   <para>This command retrieves all metrics in the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLogMetric -MetricName "metric1", "metric2" -Project "my-project"</code>
    ///   <para>
    ///   This command retrieves 2 metrics ("metric1" and "metric2") in the project "my-project".
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/view/logs_based_metrics)">
    /// [Log Metrics]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcLogMetric")]
    public class GetGcLogMetricCmdlet : GcLogCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for log metrics in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the log metrics to be retrieved.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 0)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string[] MetricName { get; set; }

        protected override void ProcessRecord()
        {
            if (MetricName != null && MetricName.Length > 0)
            {
                foreach (string metricName in MetricName)
                {
                    string formattedMetricName = PrefixProjectToMetricName(metricName, Project);
                    try
                    {
                        ProjectsResource.MetricsResource.GetRequest getRequest = Service.Projects.Metrics.Get(formattedMetricName);
                        WriteObject(getRequest.Execute());
                    }
                    catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        WriteResourceMissingError(
                            exceptionMessage: $"Metric '{metricName}' does not exist in project '{Project}'.",
                            errorId: "MetricNotFound",
                            targetObject: metricName);
                    }
                }
            }
            else
            {
                ProjectsResource.MetricsResource.ListRequest listRequest = Service.Projects.Metrics.List($"projects/{Project}");
                do
                {
                    ListLogMetricsResponse response = listRequest.Execute();
                    if (response.Metrics != null)
                    {
                        WriteObject(response.Metrics, true);
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                while (!Stopping && listRequest.PageToken != null);
            }
        }
    }

    /// <summary>
    /// Base class for cmdlet that create or update log metrics (both API have the same parameters).
    /// </summary>
    public abstract class CreateOrUpdateGcLogMetricCmdlet : GcLogEntryCmdletWithLogFilter
    {
        /// <summary>
        /// <para type="description">
        /// The project to create the metrics in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the metric. This name must be unique within the project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string MetricName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The description of the metric.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 1)]
        public string Description { get; set; }

        /// <summary>
        /// Given a log metric body, returns request that returns a LogMetric when executed.
        /// </summary>
        protected abstract LoggingBaseServiceRequest<LogMetric> GetRequest(LogMetric logMetric);

        protected override void ProcessRecord()
        {
            LogMetric logMetric = new LogMetric()
            {
                Name = MetricName,
                Description = Description
            };

            logMetric.Filter = ConstructLogFilterString(
                logName: PrefixProjectToLogName(LogName, Project),
                logSeverity: Severity,
                selectedType: SelectedResourceType,
                before: Before,
                after: After,
                otherFilter: Filter);

            try
            {
                LoggingBaseServiceRequest<LogMetric> request = GetRequest(logMetric);
                LogMetric result = request.Execute();
                WriteObject(result);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteResourceExistsError(
                    exceptionMessage: $"Cannot create '{MetricName}' in project '{Project}' because it already exists.",
                    errorId: "MetricAlreadyExists",
                    targetObject: MetricName);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new log metric.
    /// </para>
    /// <para type="description">
    /// Creates a new log metric. The metric will be created in the default project if -Project is not used.
    /// Will raise an error if the metric already exists.
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcLogMetric -MetricName "my-metric" -LogName "my-log"</code>
    ///   <para>This command creates a metric to count the number of log entries in log "my-log".</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcLogMetric -MetricName "my-metric" `
    ///                           -ResourceType "gce_instance"
    ///                           -After [DateTime]::Now().AddDays(1)
    ///                           -Project "my-project"
    ///   </code>
    ///   <para>
    ///   This command creates a metric name "my-metric" in project "my-project" that counts every log entry
    ///   of the resource type "gce_instance" that is created from tomorrow.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcLogMetric -MetricName "my-metric" -Filter 'textPayload = "textPayload"'
    ///   </code>
    ///   <para>
    ///   This command creates a metric name "my-metric" that counts every log entry that matches the provided filter.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/view/logs_based_metrics)">
    /// [Log Metrics]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcLogMetric")]
    public class NewGcLogMetricCmdlet : CreateOrUpdateGcLogMetricCmdlet
    {
        protected override LoggingBaseServiceRequest<LogMetric> GetRequest(LogMetric logMetric)
        {
            if (string.IsNullOrWhiteSpace(logMetric.Filter))
            {
                throw new PSArgumentNullException(
                    "Cannot construct filter for the metric." +
                    "Please use either -LogName, -Severity, -ResourceType, -Before, -After or -Filter parameters.");
            }

            return Service.Projects.Metrics.Create(logMetric, $"projects/{Project}");
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates a log metric.
    /// </para>
    /// <para type="description">
    /// Updates a log metric. The cmdlet will create the metric if it does not exist.
    /// The default project will be used to search for the metric if -Project is not used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Set-GcLogMetric -MetricName "my-metric" -LogName "my-log"</code>
    ///   <para>This command updates the metric "my-metric" to count the number of log entries in log "my-log".</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GcLogMetric -MetricName "my-metric" `
    ///                           -ResourceType "gce_instance"
    ///                           -After [DateTime]::Now().AddDays(1)
    ///                           -Project "my-project"
    ///   </code>
    ///   <para>
    ///   This command updates the metric name "my-metric" in project "my-project" to count every log entry
    ///   of the resource type "gce_instance" that is created from tomorrow.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> Set-GcLogMetric -MetricName "my-metric" -Filter 'textPayload = "textPayload"'
    ///   </code>
    ///   <para>
    ///   This command updates the metric name "my-metric" to count every log entry that matches the provided filter.
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/view/logs_based_metrics)">
    /// [Log Metrics]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcLogMetric")]
    public class SetGcLogMetricCmdlet : CreateOrUpdateGcLogMetricCmdlet
    {
        protected override LoggingBaseServiceRequest<LogMetric> GetRequest(LogMetric logMetric)
        {
            string formattedMetricName = PrefixProjectToMetricName(MetricName, Project);

            // If user does not supply filter or description for update request, we have to use the existing metric's filter.
            if (string.IsNullOrWhiteSpace(logMetric.Filter) || string.IsNullOrWhiteSpace(logMetric.Description))
            {
                try
                {
                    ProjectsResource.MetricsResource.GetRequest getRequest = Service.Projects.Metrics.Get(formattedMetricName);
                    LogMetric existingMetric = getRequest.Execute();
                    if (string.IsNullOrWhiteSpace(logMetric.Filter))
                    {
                        logMetric.Filter = existingMetric.Filter;
                    }

                    if (string.IsNullOrWhiteSpace(logMetric.Description))
                    {
                        logMetric.Description = existingMetric.Description;
                    }
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    // We don't need to throw for description since it's optional.
                    if (string.IsNullOrWhiteSpace(logMetric.Filter))
                    {
                        throw new PSArgumentNullException(
                            "Cannot construct filter for the metric." +
                            "Please use either -LogName, -Severity, -ResourceType, -Before, -After or -Filter parameters.");
                    }
                }
            }
            return Service.Projects.Metrics.Update(logMetric, PrefixProjectToMetricName(MetricName, Project));
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes one or more log metrics from a project.
    /// </para>
    /// <para type="description">
    /// Removes one or more log metrics from a project based on the name of the metrics.
    /// If -Project is not specified, the default project will be used.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcLogMetric -MetricName "my-metric"</code>
    ///   <para>This command removes "my-metric" from the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcLogMetric -MetricName "my-metric", "my-metric2" -Project "my-project"</code>
    ///   <para>This command removes "my-metric" and "my-metric2" from project "my-project".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/view/logs_based_metrics)">
    /// [Log Metrics]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcLogMetric", SupportsShouldProcess = true)]
    public class RemoveGcLogMetricCmdlet : GcLogCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for log metrics in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the metrics to be removed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [ArrayPropertyTransform(typeof(LogMetric), nameof(LogMetric.Name))]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string[] MetricName { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string metric in MetricName)
            {
                string formattedLogMetric = PrefixProjectToMetricName(metric, Project);
                try
                {
                    if (ShouldProcess(formattedLogMetric, "Remove Log Metric"))
                    {
                        ProjectsResource.MetricsResource.DeleteRequest request = Service.Projects.Metrics.Delete(formattedLogMetric);
                        request.Execute();
                    }
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        exceptionMessage: $"Metric '{metric}' does not exist in project '{Project}'.",
                        errorId: "MetricNotFound",
                        targetObject: metric);
                }
            }
        }
    }
}
