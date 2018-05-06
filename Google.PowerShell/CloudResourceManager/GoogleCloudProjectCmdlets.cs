// Copyright 2018 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.CloudResourceManager.v1;
using Google.Apis.CloudResourceManager.v1.Data;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;

namespace Google.PowerShell.CloudResourceManager
{
    /// <summary>
    /// <para type="synopsis">
    /// Retrieves one or more Google Cloud projects.
    /// </para>
    /// <para type="description">
    /// Retrieves one or more Google Cloud projects. The cmdlet will return all available projects if no parameter
    /// is used. If -Name, -Id or -Label is used, the cmdlets will return the projects that match the given arguments.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcProject</code>
    ///   <para>This command gets all the available Google Cloud projects.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcProject -Name "My Project"</code>
    ///   <para>This command gets the project that has the name "My Project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcProject -Id "my-id"</code>
    ///   <para>This command gets the project that has the Id "my-id".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcProject -Label @{"environment" = "test"}</code>
    ///   <para>This command gets all the projects that has the label "environment" with value "test".</para>
    /// </example>
    /// <para
    ///     type="link"
    ///     uri="(https://cloud.google.com/resource-manager/docs/cloud-platform-resource-hierarchy#cloud_platform_projects)">
    /// [Projects]
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/resource-manager/docs/using-labels)">
    /// [Labels]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcpProject")]
    public class GetGcpProjectCmdlet : CloudResourceManagerCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The name of the project to seach for.
        /// This parameter is case insensitive.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The Id of the project to seach for.
        /// This parameter is case insensitive.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string ProjectId { get; set; }

        /// <summary>
        /// <para type="description">
        /// The labels of the project to seach for.
        /// Key and value of the label should be in lower case with no spaces in them.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public Hashtable Label { get; set; }

        protected override void ProcessRecord()
        {
            string filter = ConstructFilter(Name, ProjectId, Label);
            WriteObject(GetProjects(filter), true);
        }

        /// <summary>
        /// Constructs filter for the list request based on name, projectid and label.
        /// </summary>
        private string ConstructFilter(string name, string projectId, Hashtable labels)
        {
            // Filter is case insensitive
            StringBuilder filter = new StringBuilder();
            if (name != null)
            {
                filter.Append($"name:'{name}'");
            }

            if (projectId != null)
            {
                filter.Append($" id:'{projectId}'");
            }

            if (labels != null && labels.Count > 0)
            {
                // Each label is represented in the filter as labels.key:value.
                foreach (string key in labels.Keys)
                {
                    string value = labels[key]?.ToString();
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    {
                        throw new PSArgumentException(
                            "Label dictionary should not have null or empty key or value",
                            nameof(labels));
                    }

                    // The key and value needs to be in lower case and they cannot have space.
                    string formattedKey = key.Trim().ToLower();
                    value = value.Trim().ToLower();
                    if (formattedKey.Contains(" ") || value.Contains(" "))
                    {
                        throw new PSArgumentException("Label key and value cannot contain white spaces", nameof(labels));
                    }

                    filter.Append($" labels.{key.ToLower()}:{value.ToLower()}");
                }
            }
            return filter.ToString().Trim().Replace('\'', '"');
        }

        /// <summary>
        /// Returns list of projects based on the filter.
        /// </summary>
        private IEnumerable<Project> GetProjects(string filter)
        {
            ProjectsResource.ListRequest request = Service.Projects.List();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                request.Filter = filter;
            }

            do
            {
                ListProjectsResponse response = request.Execute();
                if (response.Projects != null)
                {
                    foreach (Project project in response.Projects)
                    {
                        yield return project;
                    }
                }
                request.PageToken = response.NextPageToken;
            }
            while (!Stopping && request.PageToken != null);
        }
    }
}
