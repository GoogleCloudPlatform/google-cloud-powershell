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
    /// Retrieves an instance operation that has been performed on an instance,
    /// or a list of operations used on the instance.
    /// </para>
    /// <para type="description">
    /// Retrieves an instance operation that has been performed on an instance,
    /// or a list of operations used on the instance. This is decided by if you provide a Name or not.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlOperation")]
    public class GetGcSqlOperationCmdlet : GcSqlCmdlet
    {
        internal class ParameterSetNames
        {
            public const string GetSingle = "Single";
            public const string GetList = "List";
        }

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
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.GetSingle)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// Cloud SQL instance ID. this does not include the project ID.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.GetList)]
        public string Instance { get; set; }

        protected override void ProcessRecord()
        {
            if (Name != null)
            {
                OperationsResource.GetRequest request = Service.Operations.Get(Project, Name);
                Operation result = request.Execute();
                WriteObject(result);
            }
            else
            {
                OperationsResource.ListRequest request = Service.Operations.List(Project, Instance);
                OperationsListResponse result = request.Execute();
                WriteObject(result.Items, true);
            }
        }
    }
}
