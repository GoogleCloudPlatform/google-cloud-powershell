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
        /// Project name of the project for which to get an operation.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.GetSingle)]
        [Parameter(ParameterSetName = ParameterSetNames.GetList)]
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
        /// Cloud SQL instance name.
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
                IEnumerable<Operation> results = GetAllOperations();
                WriteObject(results, true);
            }
        }

        private IEnumerable<Operation> GetAllOperations()
        {
            OperationsResource.ListRequest request = Service.Operations.List(Project, Instance);
            do
            {
                OperationsListResponse aggList = request.Execute();
                IList<Operation> operations = aggList.Items;
                if (operations == null)
                {
                    yield break;
                }
                foreach (Operation operation in operations)
                {
                    yield return operation;
                }
                request.PageToken = aggList.NextPageToken;
            }
            while (request.PageToken != null);
        }
    }
}
