// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Logging.v2;
using Google.Apis.Logging.v2.Data;
using Google.PowerShell.Common;
using System.Management.Automation;
using System.Net;

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
}
