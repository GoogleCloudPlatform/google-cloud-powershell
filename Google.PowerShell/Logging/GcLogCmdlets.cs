// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Logging.v2;
using Google.Apis.Logging.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Xml;

namespace Google.PowerShell.Logging
{
    /// <summary>
    /// Base class for Stackdriver Logging cmdlets.
    /// </summary>
    public class GcLogCmdlet : GCloudCmdlet
    {
        /// <summary>
        /// Enum of severity levels for a log entry.
        /// </summary>
        public enum LogSeverity
        {
            /// <summary>
            /// The log entry has no assigned severity level.
            /// </summary>
            Default,

            /// <summary>
            /// Debug or trace information.
            /// </summary>
            Debug,

            /// <summary>
            /// Routine information, such as ongoing status or performance.
            /// </summary>
            Info,

            /// <summary>
            /// Normal but significant events, such as start up, shut down, or a configuration change.
            /// </summary>
            Notice,

            /// <summary>
            /// Warning events might cause problems.
            /// </summary>
            Warning,

            /// <summary>
            /// Error events are likely to cause problems.
            /// </summary>
            Error,

            /// <summary>
            /// Critical events cause more severe problems or outages.
            /// </summary>
            Critical,

            /// <summary>
            /// A person must take an action immediately.
            /// </summary>
            Alert,

            /// <summary>
            /// One or more systems are unusable.
            /// </summary>
            Emergency
        }

        public LoggingService Service { get; private set; }

        public GcLogCmdlet()
        {
            Service = new LoggingService(GetBaseClientServiceInitializer());
        }

        /// <summary>
        /// Prefix projects/{project id}/logs to logName if not present.
        /// </summary>
        protected string PrefixProjectToLogName(string logName, string project)
        {
            if (!string.IsNullOrWhiteSpace(logName) && !logName.StartsWith($"projects/{project}/logs"))
            {
                logName = $"projects/{project}/logs/{logName}";
            }
            return logName;
        }

        /// <summary>
        /// Prefix projects/{project id}/sinks to sinkName if not present.
        /// </summary>
        protected string PrefixProjectToSinkName(string sinkName, string project)
        {
            if (!string.IsNullOrWhiteSpace(sinkName) && !sinkName.StartsWith($"projects/{project}/sinks"))
            {
                sinkName = $"projects/{project}/sinks/{sinkName}";
            }
            return sinkName;
        }

        /// <summary>
        /// Prefix projects/{project id}/metrics to metricName if not present.
        /// </summary>
        protected string PrefixProjectToMetricName(string metricName, string project)
        {
            if (!string.IsNullOrWhiteSpace(metricName) && !metricName.StartsWith($"projects/{project}/metrics"))
            {
                metricName = $"projects/{project}/metrics/{metricName}";
            }
            return metricName;
        }

        /// <summary>
        /// A cache of the list of valid monitored resource descriptors.
        /// This is used for auto-completion to display possible types of monitored resource.
        /// </summary>
        private static Lazy<List<MonitoredResourceDescriptor>> s_monitoredResourceDescriptors =
            new Lazy<List<MonitoredResourceDescriptor>>(GetResourceDescriptors);

        /// <summary>
        /// Gets all possible monitored resource descriptors.
        /// </summary>
        private static List<MonitoredResourceDescriptor> GetResourceDescriptors()
        {
            List<MonitoredResourceDescriptor> monitoredResourceDescriptors = new List<MonitoredResourceDescriptor>();
            LoggingService service = new LoggingService(GetBaseClientServiceInitializer());
            MonitoredResourceDescriptorsResource.ListRequest request = service.MonitoredResourceDescriptors.List();
            do
            {
                ListMonitoredResourceDescriptorsResponse response = request.Execute();
                if (response.ResourceDescriptors != null)
                {
                    monitoredResourceDescriptors.AddRange(response.ResourceDescriptors);
                }
                request.PageToken = response.NextPageToken;
            }
            while (request.PageToken != null);
            return monitoredResourceDescriptors;
        }

        /// <summary>
        /// Returns a monitored resource descriptor based on a given type.
        /// </summary>
        protected MonitoredResourceDescriptor GetResourceDescriptor(string descriptorType)
        {
            return s_monitoredResourceDescriptors.Value.First(
                descriptor => string.Equals(descriptor.Type.ToLower(), descriptorType.ToLower()));
        }

        /// <summary>
        /// Returns all valid resource types.
        /// </summary>
        protected string[] AllResourceTypes => s_monitoredResourceDescriptors.Value.Select(descriptor => descriptor.Type).ToArray();

        /// <summary>
        /// Generate -ResourceType dynamic parameter. Cmdlets can use this parameter to filter log entries based on resource types
        /// such as "gce_instance". For a full list of resource types, see https://cloud.google.com/logging/docs/api/v2/resource-list
        /// </summary>
        protected RuntimeDefinedParameter GenerateResourceTypeParameter(bool mandatory)
        {
            return GenerateRuntimeParameter(
                parameterName: "ResourceType",
                helpMessage: "If specified, the cmdlet will filter out log entries based on the resource type.",
                validSet: AllResourceTypes,
                isMandatory: mandatory);
        }

        /// <summary>
        /// Constructs a filter string based on log name, severity, type of log, before and after timestamps
        /// and other advanced filter.
        /// </summary>
        protected string ConstructLogFilterString(string logName, LogSeverity? logSeverity, string selectedType,
            DateTime? before, DateTime? after, string otherFilter)
        {
            string andOp = " AND ";
            string filterString = "";

            if (!string.IsNullOrWhiteSpace(logName))
            {
                // By setting logName = LogName in the filter, the list request
                // will only return log entry that belongs to LogName.
                // Example: logName = "Name of log".
                filterString = $"logName = '{logName}'{andOp}".Replace('\'', '"');
            }

            if (logSeverity.HasValue)
            {
                // Example: severity >= ERROR.
                string severityString = Enum.GetName(typeof(LogSeverity), logSeverity.Value).ToUpper();
                filterString += $"severity = {severityString}{andOp}";
            }

            if (selectedType != null)
            {
                // Example: resource.type = "gce_instance".
                filterString += $"resource.type = '{selectedType}'{andOp}".Replace('\'', '"');
            }

            if (before.HasValue)
            {
                // Example: timestamp <= "2016-06-27T14:40:00-04:00".
                string beforeTimestamp = XmlConvert.ToString(before.Value, XmlDateTimeSerializationMode.Local);
                filterString += $"timestamp <= '{beforeTimestamp}'{andOp}".Replace('\'', '"');
            }

            if (after.HasValue)
            {
                // Example: timestamp >= "2016-06-27T14:40:00-04:00".
                string afterTimestamp = XmlConvert.ToString(after.Value, XmlDateTimeSerializationMode.Local);
                filterString += $"timestamp >= '{afterTimestamp}'{andOp}".Replace('\'', '"');
            }

            if (otherFilter != null)
            {
                filterString += otherFilter;
            }

            // Strip the "AND " at the end if we have it.
            if (filterString.EndsWith(andOp))
            {
                filterString = filterString.Substring(0, filterString.Length - andOp.Length);
            }

            return filterString;
        }
    }

    /// <summary>
    /// Base class for GcLog cmdlet that uses log filter.
    /// </summary>
    public class GcLogEntryCmdletWithLogFilter : GcLogCmdlet, IDynamicParameters
    {
        /// <summary>
        /// <para type="description">
        /// If specified, the cmdlet will filter out log entries that are in the log LogName.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string LogName { get; set; }

        /// <summary>
        /// <para type="description">
        /// If specified, the cmdlet will filter out log entries with the specified severity.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public LogSeverity? Severity { get; set; }

        /// <summary>
        /// <para type="description">
        /// If specified, the cmdlet will filter out log entries that occur before this datetime.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public virtual DateTime? Before { get; set; }

        /// <summary>
        /// <para type="description">
        /// If specified, the cmdlet will filter out log entries that occur after this datetime.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public virtual DateTime? After { get; set; }

        /// <summary>
        /// <para type="description">
        /// If specified, the cmdlet will filter out log entries that satisfy the filter.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Filter { get; set; }

        /// <summary>
        /// This dynamic parameter dictionary is used by PowerShell to generate parameters dynamically.
        /// </summary>
        private RuntimeDefinedParameterDictionary _dynamicParameters;

        /// <summary>
        /// This function is part of the IDynamicParameters interface.
        /// PowerShell uses it to generate parameters dynamically.
        /// We have to generate -ResourceType parameter dynamically because the array
        /// of resources that we used to validate against are not generated before compile time,
        /// i.e. [ValidateSet(ArrayGeneratedAtRunTime)] will throw an error for parameters
        /// that are not generated dynamically.
        /// </summary>
        public object GetDynamicParameters()
        {
            if (_dynamicParameters == null)
            {
                _dynamicParameters = new RuntimeDefinedParameterDictionary();
                _dynamicParameters.Add("ResourceType", GenerateResourceTypeParameter(mandatory: false));
            }

            return _dynamicParameters;
        }

        /// <summary>
        /// The value of the dynamic parameter -ResourceType. For example, if user types -ResourceType gce_instance,
        /// then this will be gce_instance.
        /// </summary>
        public string SelectedResourceType
        {
            get
            {
                if (_dynamicParameters != null && _dynamicParameters.ContainsKey("ResourceType"))
                {
                    return _dynamicParameters["ResourceType"].Value?.ToString().ToLower();
                }
                return null;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets log entries.
    /// </para>
    /// <para type="description">
    /// Gets all log entries from a project or gets the entries from a specific log.
    /// Log entries can be filtered using -LogName, -Severity, -After or -Before parameter.
    /// For advanced filtering, please use -Filter parameter.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcLogEntry</code>
    ///   <para>This command gets all the log entries for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLogEntry -Project "my-project"</code>
    ///   <para>This command gets all the log entries from the project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLogEntry -LogName "my-log"</code>
    ///   <para>This command gets all the log entries from the log named "my-backendservice".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLogEntry -LogName "my-log"</code>
    ///   <para>This command gets all the log entries from the log named "my-backendservice".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLogEntry -LogName "my-log" -Severity Error</code>
    ///   <para>This command gets all the log entries with severity ERROR from the log named "my-backendservice".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLogEntry -LogName "my-log" -Before [DateTime]::Now.AddMinutes(30)</code>
    ///   <para>
    ///   This command gets all the log entries from the log named "my-backendservice" created before 30 minutes ago.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLogEntry -LogName "my-log" -After [DateTime]::Now.AddMinutes(30)</code>
    ///   <para>
    ///   This command gets all the log entries from the log named "my-backendservice" created after 30 minutes ago.
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLogEntry -Filter 'resource.type="gce_instance" AND severity >= ERROR'</code>
    ///   <para>This command gets all the log entries that satisfy filter.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/view/logs_index)">
    /// [Log Entries and Logs]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/view/advanced_filters)">
    /// [Logs Filters]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcLogEntry")]
    [OutputType(typeof(LogEntry))]
    public class GetGcLogEntryCmdlet : GcLogEntryCmdletWithLogFilter
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        protected override void ProcessRecord()
        {
            ListLogEntriesRequest logEntriesRequest = new ListLogEntriesRequest();
            // Set resource to "projects/{Project}" so we will only find log entries in project Project.
            logEntriesRequest.ResourceNames = new List<string> { $"projects/{Project}" };
            string logName = PrefixProjectToLogName(LogName, Project);
            logEntriesRequest.Filter = ConstructLogFilterString(
                logName: logName,
                logSeverity: Severity,
                selectedType: SelectedResourceType,
                before: Before,
                after: After,
                otherFilter: Filter);

            do
            {
                EntriesResource.ListRequest listLogRequest = Service.Entries.List(logEntriesRequest);
                ListLogEntriesResponse response = listLogRequest.Execute();
                if (response.Entries != null)
                {
                    foreach (LogEntry logEntry in response.Entries)
                    {
                        WriteObject(logEntry);
                    }
                }
                logEntriesRequest.PageToken = response.NextPageToken;
            }
            while (!Stopping && logEntriesRequest.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates new monitored resources.
    /// </para>
    /// <para type="description">
    /// Creates new monitored resources. These resources are used in the Logging cmdlets such as New-GcLogEntry
    /// </para>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcLogMonitoredResource -ResourceType "gce_instance" `
    ///                                      -Labels @{"project_id" = "my-project"; "instance_id" = "my-instance"}.
    ///   </code>
    ///   <para>This command creates a new monitored resource of type "gce_instance" with specified labels.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/api/v2/resource-list)">
    /// [Monitored Resources and Labels]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcLogMonitoredResource")]
    public class NewGcLogMonitoredResource : GcLogCmdlet, IDynamicParameters
    {
        /// <summary>
        /// <para type="description">
        /// The label that applies to resource type.
        /// For a complete list, see https://cloud.google.com/logging/docs/api/v2/resource-list.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public Hashtable Labels { get; set; }

        /// <summary>
        /// This dynamic parameter dictionary is used by PowerShell to generate parameters dynamically.
        /// </summary>
        private RuntimeDefinedParameterDictionary _dynamicParameters;

        /// <summary>
        /// This function is part of the IDynamicParameters interface.
        /// PowerShell uses it to generate parameters dynamically.
        /// We have to generate -ResourceType parameter dynamically because the array
        /// of resources that we used to validate against are not generated before compile time,
        /// i.e. [ValidateSet(ArrayGeneratedAtRunTime)] will throw an error for parameters
        /// that are not generated dynamically.
        /// </summary>
        public object GetDynamicParameters()
        {
            if (_dynamicParameters == null)
            {
                _dynamicParameters = new RuntimeDefinedParameterDictionary();
                _dynamicParameters.Add("ResourceType", GenerateResourceTypeParameter(mandatory: true));
            }

            return _dynamicParameters;
        }

        protected override void ProcessRecord()
        {
            string selectedType = _dynamicParameters["ResourceType"].Value.ToString().ToLower();
            MonitoredResourceDescriptor selectedDescriptor = GetResourceDescriptor(selectedType);
            IEnumerable<string> descriptorLabels = selectedDescriptor.Labels.Select(label => label.Key);

            // Validate that the Labels passed in match what is found in the labels of the selected descriptor.
            foreach (string labelKey in Labels.Keys)
            {
                if (!descriptorLabels.Contains(labelKey))
                {
                    string descriptorLabelsString = string.Join(", ", descriptorLabels);
                    string errorMessage = $"Label '{labelKey}' cannot be found for monitored resource of type '{selectedType}'."
                        + $"The available lables are '{descriptorLabelsString}'.";
                    ErrorRecord errorRecord = new ErrorRecord(
                        new ArgumentException(errorMessage),
                        "InvalidLabel",
                        ErrorCategory.InvalidData,
                        labelKey);
                    ThrowTerminatingError(errorRecord);
                }
            }

            MonitoredResource createdResource = new MonitoredResource()
            {
                Type = selectedType,
                Labels = ConvertToDictionary<string, string>(Labels)
            };
            WriteObject(createdResource);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates new log entries.
    /// </para>
    /// <para type="description">
    /// Creates new log entries in a log. The cmdlet will create the log if it doesn't exist.
    /// By default, the log is associated with the "global" resource type ("custom.googleapis.com" in v1 service).
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcLogEntry -TextPayload "This is a log entry." -LogName "test-log"</code>
    ///   <para>This command creates a log entry with the specified text payload in the log "test-log".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GcLogEntry -TextPayload "Entry 1", "Entry 2" -LogName "test-log"</code>
    ///   <para>
    ///   This command creates 2 log entries with text payload "Entry 1" and "Entry 2" respectively in the log "test-log".
    ///   </para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GcLogEntry -JsonPayload @{"a" = "b"} -LogName "test-log" -Severity Error</code>
    ///   <para>This command creates a log entry with a json payload and severity level Error in the log "test-log".</para>
    /// </example>
    /// <example>
    ///   <code>
    ///   PS C:\> New-GcLogEntry -MonitoredResource (New-GcLogMonitoredResource -ResourceType global -Labels @{"project_id" = "my-project"}) `
    ///                          -TextPayload "This is a log entry."
    ///   </code>
    ///   <para>
    ///   This command creates a log entry directly from the LogEntry object.
    ///   The command also associates it with a resource type created from New-GcLogMonitoredResource
    ///   </para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/view/logs_index)">
    /// [Log Entries and Logs]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/api/v2/resource-list)">
    /// [Monitored Resources]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcLogEntry", DefaultParameterSetName = ParameterSetNames.TextPayload)]
    public class NewGcLogEntryCmdlet : GcLogCmdlet
    {
        private class ParameterSetNames
        {
            public const string TextPayload = "TextPayload";
            public const string JsonPayload = "JsonPayload";
            public const string ProtoPayload = "ProtoPayload";
        }

        /// <summary>
        /// <para type="description">
        /// The project to where the log entry will be written to. If not set via PowerShell parameter processing,
        /// will default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the log that this entry will be written to.
        /// If the log does not exist, it will be created.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        public string LogName { get; set; }

        /// <summary>
        /// <para type="description">
        /// The text payload of the log entry. Each value in the array will be written to a single entry in the log.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.TextPayload, Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public string[] TextPayload { get; set; }

        /// <summary>
        /// <para type="description">
        /// The JSON payload of the log entry. Each value in the array will be written to a single entry in the log.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.JsonPayload, Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public Hashtable[] JsonPayload { get; set; }

        /// <summary>
        /// <para type="description">
        /// The proto payload of the log entry. Each value in the array will be written to a single entry in the log.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ProtoPayload, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public Hashtable[] ProtoPayload { get; set; }

        /// <summary>
        /// <para type="description">
        /// The severity of the log entry. Default value is Default.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public LogSeverity Severity { get; set; }

        /// <summary>
        /// <para type="description">
        /// Monitored Resource associated with the log. If not provided, we will default to "global" resource type
        /// ("custom.googleapis.com" in v1 service). This is what gcloud beta logging write uses.
        /// This indicates that the log is not associated with any specific resource.
        /// More information can be found at https://cloud.google.com/logging/docs/api/v2/resource-list
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public MonitoredResource MonitoredResource { get; set; }

        protected override void ProcessRecord()
        {
            LogName = PrefixProjectToLogName(LogName, Project);
            if (MonitoredResource == null)
            {
                MonitoredResource = new MonitoredResource()
                {
                    Type = "global",
                    Labels = new Dictionary<string, string>() { { "project_id", Project } }
                };
            }
            List<LogEntry> entries = new List<LogEntry>();

            switch (ParameterSetName)
            {
                case ParameterSetNames.TextPayload:
                    foreach (string text in TextPayload)
                    {
                        LogEntry entry = new LogEntry()
                        {
                            LogName = LogName,
                            Severity = Enum.GetName(typeof(LogSeverity), Severity),
                            Resource = MonitoredResource,
                            TextPayload = text
                        };
                        entries.Add(entry);
                    }
                    break;
                case ParameterSetNames.ProtoPayload:
                    foreach (Hashtable hashTable in ProtoPayload)
                    {
                        LogEntry entry = new LogEntry()
                        {
                            LogName = LogName,
                            Severity = Enum.GetName(typeof(LogSeverity), Severity),
                            Resource = MonitoredResource,
                            ProtoPayload = ConvertToDictionary<string, object>(hashTable)
                        };
                        entries.Add(entry);
                    }
                    break;
                case ParameterSetNames.JsonPayload:
                    foreach (Hashtable hashTable in JsonPayload)
                    {
                        LogEntry entry = new LogEntry()
                        {
                            LogName = LogName,
                            Severity = Enum.GetName(typeof(LogSeverity), Severity),
                            Resource = MonitoredResource,
                            JsonPayload = ConvertToDictionary<string, object>(hashTable)
                        };
                        entries.Add(entry);
                    }
                    break;
                default:
                    throw UnknownParameterSetException;
            }

            WriteLogEntriesRequest writeRequest = new WriteLogEntriesRequest()
            {
                Entries = entries,
                LogName = LogName,
                Resource = MonitoredResource
            };
            EntriesResource.WriteRequest request = Service.Entries.Write(writeRequest);
            WriteLogEntriesResponse response = request.Execute();
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Lists Stackdriver logs' names from a project.
    /// </para>
    /// <para type="description">
    /// Lists Stackdriver logs' names from a project. Will display logs' names from the default project if -Project is not used.
    /// A log is a named collection of log entries within the project (any log mus thave at least 1 log entry).
    /// To get log entries from a particular log, use Get-GcLogEntry cmdlet instead.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcLog</code>
    ///   <para>This command gets logs from the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcLog -Project "my-project"</code>
    ///   <para>This command gets logs from project "my-project".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/basic-concepts#logs)">
    /// [Logs]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcLog")]
    public class GetGcLogCmdlet : GcLogCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for logs in. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        protected override void ProcessRecord()
        {
            ProjectsResource.LogsResource.ListRequest listRequest = Service.Projects.Logs.List($"projects/{Project}");
            do
            {
                try
                {
                    ListLogsResponse response = listRequest.Execute();
                    if (response.LogNames != null)
                    {
                        WriteObject(response.LogNames, true);
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new PSArgumentException($"Project {Project} does not exist.");
                }
            }
            while (!Stopping && listRequest.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes one or more Stackdriver logs from a project.
    /// </para>
    /// <para type="description">
    /// Removes one or more StackDrive logs from a project based on the names of the logs.
    /// All the entries in the logs will be deleted (a log have multiple log entries).
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcLog -LogName "test-log"</code>
    ///   <para>This command removes "test-log" from the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcLog -LogName "test-log" -Project "my-project"</code>
    ///   <para>This command removes "test-log" from project "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GcLog -LogName "log1", "log2"</code>
    ///   <para>This command removes "log1" and "log2" from the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/view/logs_index)">
    /// [Log Entries and Logs]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcLog", SupportsShouldProcess = true)]
    public class RemoveGcLogCmdlet : GcLogCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names of the logs to be removed.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public string[] LogName { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string log in LogName)
            {
                string formattedLogName = PrefixProjectToLogName(log, Project);
                try
                {
                    if (ShouldProcess(formattedLogName, "Remove Log"))
                    {
                        ProjectsResource.LogsResource.DeleteRequest deleteRequest = Service.Projects.Logs.Delete(formattedLogName);
                        deleteRequest.Execute();
                    }
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    WriteResourceMissingError(
                        exceptionMessage: $"Log '{log}' does not exist in project '{Project}'.",
                        errorId: "LogNotFound",
                        targetObject: log);
                }
            }
        }
    }
}
