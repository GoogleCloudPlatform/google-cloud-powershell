// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using System.Management.Automation;
using Google.PowerShell.Common;

namespace Google.PowerShell.Sql
{
    /// <summary>
    /// <para type="synopsis">
    /// Retrieves a resource containing information about a database inside a Cloud SQL instance,
    /// or lists all databases inside a Cloud SQL instance.
    /// 
    /// This is only supported by first-generation instances.
    /// </para>
    /// <para type="description">
    /// Retrieves a resource containing information about a database inside a Cloud SQL instance,
    /// or lists all databases inside a Cloud SQL instance. This is decided by if you provide a Database or not.
    /// 
    /// This is only supported by first-generation instances.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlDatabase")]
    public class GetGcSqlDatabaseCmdlet : GcSqlCmdlet
    {
        internal class ParameterSetNames
        {
            public const string GetSingle = "Single";
            public const string GetList = "List";
        }

        /// <summary>
        /// <para type="description">
        /// Project name of the project that contains an instance.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.GetSingle)]
        [Parameter(ParameterSetName = ParameterSetNames.GetList)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance name. 
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.GetSingle)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.GetList)]
        public string Instance { get; set; }

        /// <summary>
        /// <para type="description">
        /// Name of the database to be retrieved in the instance.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.GetList)]
        public string Database { get; set; }

        protected override void ProcessRecord()
        {
            if (Service.Instances.Get(Project, Instance).Execute().BackendType != "FIRST_GEN")
            {
                throw new GoogleApiException("Google Cloud SQL Api", "Database cmdlets are only supported by First-Generation instances");
            }
            else if (Database != null)
            {
                 DatabasesResource.GetRequest request = Service.Databases.Get(Project, Instance, Database);
                 Database result = request.Execute();
                 WriteObject(result);
            }
            else
            {
                 DatabasesResource.ListRequest request = Service.Databases.List(Project, Instance);
                 DatabasesListResponse result = request.Execute();
                 WriteObject(result.Items, true);
            }
        }
    }
}
