// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using System.Management.Automation;
using Google.PowerShell.Common;
using Google.PowerShell.Sql;

namespace Google.PowerShell.Sql
{

    /// <summary>
    /// <para type="synopsis">
    /// Retrieves an instance operation that has been performed on an instance.
    /// </para>
    /// <para type="description">
    /// Retrieves the specified instance operation.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlOperation")]
    public class GetGcSqlOperationCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Project ID of the project for which to get an operation.
        /// </para>
        /// </summary>
        [Parameter(Position = 0)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Instance operation ID/name.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string OperationName { get; set; }

        protected override void ProcessRecord()
        {
            OperationsResource.GetRequest request = Service.Operations.Get(Project, OperationName);
            var result = request.Execute();
            WriteObject(result, true);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Lists all instance operations that have been performed on the given Cloud SQL instance,
    /// in the reverse chronological order of the start time.
    /// </para>
    /// <para type="description">
    /// Lists all instance operations that have been performed on the given Cloud SQL instance.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlOperations")]
    public class GetGcSqlOperationsCmdlet : GcSqlCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Project ID of the project that contains the instance.
        /// </para>
        /// </summary>
        [Parameter(Position = 0)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance ID. this does not include the project ID.
        /// </para>
        /// </summary>
        [Parameter(Position = 1, Mandatory = true)]
        public string Instance { get; set; }

        protected override void ProcessRecord()
        {
            OperationsResource.ListRequest request = Service.Operations.List(Project, Instance);
            var result = request.Execute();
            WriteObject(result.Items, true);
        }
    }
}
