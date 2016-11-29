using Google.Apis.Logging.v2;
using Google.Apis.Logging.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
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
        protected string PrefixProject(string logName, string project)
        {
            if (!string.IsNullOrWhiteSpace(logName) && !logName.StartsWith($"projects/{project}/logs"))
            {
                logName = $"projects/{project}/logs/{logName}";
            }
            return logName;
        }

        /// <summary>
        /// Converts a hashtable to a dictionary
        /// </summary>
        protected Dictionary<K,V> ConvertToDictionary<K,V>(Hashtable hashTable)
        {
            return hashTable.Cast<DictionaryEntry>().ToDictionary(kvp => (K)kvp.Key, kvp => (V)kvp.Value);
        }

        /// <summary>
        /// A cache of the list of valid monitored resource descriptors.
        /// This is used for auto-completion to display possible types of monitored resource.
        /// </summary>
        private static Lazy<List<MonitoredResourceDescriptor>> monitoredResourceDescriptors =
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
            return monitoredResourceDescriptors.Value.First(
                descriptor => string.Equals(descriptor.Type.ToLower(), descriptorType.ToLower()));
        }

        /// <summary>
        /// Returns all valid resource types.
        /// </summary>
        protected string[] AllResourceTypes => monitoredResourceDescriptors.Value.Select(descriptor => descriptor.Type).ToArray();
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
    [Cmdlet(VerbsCommon.Get, "GcLogEntry", DefaultParameterSetName = ParameterSetNames.NoFilter)]
    [OutputType(typeof(LogEntry))]
    public class GetGcLogEntryCmdlet : GcLogCmdlet, IDynamicParameters
    {
        private class ParameterSetNames
        {
            public const string Filter = "Filter";
            public const string NoFilter = "NoFilter";
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
        /// If specified, the cmdlet will only return log entries in the log with the same name.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.NoFilter)]
        public string LogName { get; set; }

        /// <summary>
        /// <para type="description">
        /// If specified, the cmdlet will only return log entries with the specified severity.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.NoFilter)]
        public LogSeverity? Severity { get; set; }

        /// <summary>
        /// <para type="description">
        /// If specified, the cmdlet will only return log entries that occur before this datetime.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.NoFilter)]
        public DateTime? Before { get; set; }

        /// <summary>
        /// <para type="description">
        /// If specified, the cmdlet will only return log entries that occur after this datetime.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.NoFilter)]
        public DateTime? After { get; set; }

        /// <summary>
        /// <para type="description">
        /// If specified, the cmdlet will use this to filter out log entries returned.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.Filter)]
        [ValidateNotNullOrEmpty]
        public string Filter { get; set; }

        /// <summary>
        /// This dynamic parameter dictionary is used by PowerShell to generate parameters dynamically.
        /// </summary>
        private RuntimeDefinedParameterDictionary dynamicParameters;

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
            if (dynamicParameters == null)
            {
                ParameterAttribute paramAttribute = new ParameterAttribute()
                {
                    Mandatory = false,
                    ParameterSetName = ParameterSetNames.NoFilter
                };
                ValidateSetAttribute validateSetAttribute = new ValidateSetAttribute(AllResourceTypes);
                validateSetAttribute.IgnoreCase = true;
                Collection<Attribute> attributes =
                    new Collection<Attribute>(new Attribute[] { validateSetAttribute, paramAttribute });
                // This parameter can now be thought of as:
                // [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.NoFilter)]
                // [ValidateSet(validTypeValues)]
                // public string { get; set; }
                RuntimeDefinedParameter typeParameter = new RuntimeDefinedParameter("ResourceType", typeof(string), attributes);
                dynamicParameters = new RuntimeDefinedParameterDictionary();
                dynamicParameters.Add("ResourceType", typeParameter);
            }

            return dynamicParameters;
        }

        protected override void ProcessRecord()
        {
            ListLogEntriesRequest logEntriesRequest = new ListLogEntriesRequest();
            // Set resource to "projects/{Project}" so we will only find log entries in project Project.
            logEntriesRequest.ResourceNames = new List<string> { $"projects/{Project}" };

            if (ParameterSetName == ParameterSetNames.Filter)
            {
                logEntriesRequest.Filter = Filter;
            }
            else
            {
                string andOp = " AND ";
                string filterString = "";

                if (!string.IsNullOrWhiteSpace(LogName))
                {
                    LogName = PrefixProject(LogName, Project);
                    // By setting logName = LogName in the filter, the list request
                    // will only return log entry that belongs to LogName.
                    // Example: logName = "Name of log".
                    filterString = $"logName = '{LogName}'{andOp}".Replace('\'', '"');
                }

                if (Severity.HasValue)
                {
                    // Example: severity >= ERROR.
                    string severityString = Enum.GetName(typeof(LogSeverity), Severity.Value).ToUpper();
                    filterString += $"severity = {severityString}{andOp}";
                }

                string selectedType = dynamicParameters["ResourceType"].Value?.ToString().ToLower();
                if (selectedType != null)
                {
                    // Example: resource.type = "gce_instance".
                    filterString += $"resource.type = '{selectedType}'{andOp}".Replace('\'', '"');
                }

                if (Before.HasValue)
                {
                    // Example: timestamp <= "2016-06-27T14:40:00-04:00".
                    string beforeTimestamp = XmlConvert.ToString(Before.Value, XmlDateTimeSerializationMode.Local);
                    filterString += $"timestamp <= '{beforeTimestamp}'{andOp}".Replace('\'', '"');
                }

                if (After.HasValue)
                {
                    // Example: timestamp >= "2016-06-27T14:40:00-04:00".
                    string afterTimestamp = XmlConvert.ToString(After.Value, XmlDateTimeSerializationMode.Local);
                    filterString += $"timestamp >= '{afterTimestamp}'{andOp}".Replace('\'', '"');
                }

                // Strip the "AND " at the end if we have it.
                if (filterString.EndsWith(andOp))
                {
                    logEntriesRequest.Filter = filterString.Substring(0, filterString.Length - andOp.Length);
                }
            }

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
    ///   PS C:\> New-GcLogMonitoredResource -ResourceType gce_instance `
    ///                                      -Labels @{"project_id" = "my-project"; "instance_id" = "my-instance"}.
    ///   </code>
    ///   <para>This command creates a new monitored resource of type gce_instance with specified labels.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/logging/docs/api/v2/resource-list)">
    /// [Monitored Resources and Labels]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcLogMonitoredResource")]
    public class NewGcLogMonitoredResource : GcLogCmdlet, IDynamicParameters
    {
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public Hashtable Labels { get; set; }

        /// <summary>
        /// This dynamic parameter dictionary is used by PowerShell to generate parameters dynamically.
        /// </summary>
        private RuntimeDefinedParameterDictionary dynamicParameters;

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
            if (dynamicParameters == null)
            {
                ParameterAttribute paramAttribute = new ParameterAttribute()
                {
                    Mandatory = true
                };
                ValidateSetAttribute validateSetAttribute = new ValidateSetAttribute(AllResourceTypes);
                validateSetAttribute.IgnoreCase = true;
                Collection<Attribute> attributes =
                    new Collection<Attribute>(new Attribute[] { validateSetAttribute, paramAttribute });
                // This parameter can now be thought of as:
                // [Parameter(Mandatory = true)]
                // [ValidateSet(validTypeValues)]
                // public string { get; set; }
                RuntimeDefinedParameter typeParameter = new RuntimeDefinedParameter("ResourceType", typeof(string), attributes);
                dynamicParameters = new RuntimeDefinedParameterDictionary();
                dynamicParameters.Add("ResourceType", typeParameter);
            }

            return dynamicParameters;
        }

        protected override void ProcessRecord()
        {
            string selectedType = dynamicParameters["ResourceType"].Value.ToString().ToLower();
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
    ///   <code>PS C:\> New-GcLogEntry -TextPayload "This is a log entry." -LogName "test-log".</code>
    ///   <para>This command creates a log entry with the specified text payload in the log "test-log".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> New-GcLogEntry -TextPayload "Entry 1", "Entry 2" -LogName "test-log".</code>
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
        public string Project { get; set; }

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
            LogName = PrefixProject(LogName, Project);
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
}
