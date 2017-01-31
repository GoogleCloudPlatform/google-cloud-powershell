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
        public string Project { get; set; }

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
    ///                           -ResourceType gce_instance
    ///                           -After [DateTime]::Now().AddDays(1)
    ///                           -Project "my-project"
    ///   </code>
    ///   <para>
    ///   This command creates a metric name "my-metric" in project "my-project" that counts every log entry
    ///   of the resource type gce_instance that is created from tomorrow.
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
    public class NewGcLogMetricCmdlet : GcLogEntryCmdletWithLogFilter
    {
        /// <summary>
        /// <para type="description">
        /// The project to create the metrics in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the metric to be created. This name must be unique within the project.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string MetricName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The description of the metric to be created.
        /// </para> 
        /// </summary>
        [Parameter(Mandatory = false, Position = 1)]
        public string Description { get; set; }

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

            if (string.IsNullOrWhiteSpace(logMetric.Filter))
            {
                throw new PSArgumentNullException(
                    "Cannot construct filter for the metric." +
                    "Please use either -LogName, -Severity, -ResourceType, -Before, -After or -Filter parameters.");
            }

            try
            {
                ProjectsResource.MetricsResource.CreateRequest createRequest =
                    Service.Projects.Metrics.Create(logMetric, $"projects/{Project}");
                LogMetric result = createRequest.Execute();
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
}
