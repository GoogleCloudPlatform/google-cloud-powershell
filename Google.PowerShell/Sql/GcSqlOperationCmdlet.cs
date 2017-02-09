// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Collections.Generic;
using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using System.Management.Automation;
using Google.PowerShell.Common;

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
    /// <example>
    ///   <code>PS C:\> Get-GcSqlOperation -Instance "myInstance"</code>
    ///   <para>Gets a list of operations done on the instance "myInstance".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GcSqlOperation -Name "1d402..."</code>
    ///   <para>Gets a resource for the operation with ID "1d402...".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlOperation")]
    [OutputType(typeof(Operation))]
    public class GetGcSqlOperationCmdlet : GcSqlCmdlet
    {
        internal class ParameterSetNames
        {
            public const string GetSingle = "Single";
            public const string GetList = "List";
        }

        /// <summary>
        /// <para type="description">
        /// Name of the project. Defaults to the Cloud SDK configuration for properties if not specified.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

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
