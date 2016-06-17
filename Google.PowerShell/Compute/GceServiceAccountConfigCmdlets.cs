// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Creates a new ServiceAccount object.
    /// </para>
    /// <para type="description">
    /// Creates a new ServiceAccount object. These objects are used by New-GceInstanceConfig and 
    /// Add-GceInstanceTempalte cmdlets to link to service accounts and define scopes.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GceServiceAccountConfig", DefaultParameterSetName = ParameterSetNames.FromFlags)]
    public class NewGceServiceAccountConfigCmdlet : GCloudCmdlet
    {
        private class ParameterSetNames
        {
            public const string FromFlags = "FromFlags";
            public const string FromScopeUris = "FromScopeUris";
        }

        /// <summary>
        /// Enum used by BigtableAdmin parameter.
        /// </summary>
        public enum BigTableAdminEnum
        {
            None,
            Tables,
            Full
        }

        /// <summary>
        /// Various possible Read and Write scopes. Not values are legal for all parameters.
        /// </summary>
        public enum ReadWrite
        {
            None,
            Read,
            Write,
            ReadWrite,
            Full
        }

        /// <summary>
        /// <para type="description">
        /// The email of the service account to link to.
        /// </para>
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ParameterSetName = ParameterSetNames.FromScopeUris)]
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.FromFlags)]
        public string Email { get; set; }

        /// <summary>
        /// <para type="description">
        /// A uri of a scope to add to this service account. When added from the pipeline, all pipeline scopes
        /// will be added to a single ServiceAccount.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.FromScopeUris)]
        public List<string> ScopeUri { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the BigQuery scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter BigQuery { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of Bigtable Admin scope. Defaults to None. Also accepts Tables and Full
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public BigTableAdminEnum BigtableAdmin { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of Bigtable Data scope. Defaults to None. Also accepts Read and ReadWrite.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        [ValidateSet("None", "Read", "ReadWrite")]
        public ReadWrite BigtableData { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the Cloud Datastore scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter CloudDatastore { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of Cloud Logging API scope to add. Defaults to Write. Also accepts None, Read and Full.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        [ValidateSet("None", "Read", "Write", "Full")]
        public ReadWrite CloudLogging { get; set; } = ReadWrite.Write;

        /// <summary>
        /// <para type="description">
        /// The type of Cloud Monitoring scope to add. Defaults to Write. Also accepts None, Read and Full.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        [ValidateSet("None", "Read", "Write", "Full")]
        public ReadWrite CloudMonitoring { get; set; } = ReadWrite.Write;

        /// <summary>
        /// <para type="description">
        /// If set, adds the Cloud Pub/Sub scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter CloudPubSub { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the Cloud SQL scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter CloudSQL { get; set; }

        /// <summary>
        /// <para type="description">
        /// The value of the Compute scope to add. Defaults to None. Also accepts Read and ReadWrite.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        [ValidateSet("None", "Read", "ReadWrite")]
        public ReadWrite Compute { get; set; }

        /// <summary>
        /// <para type="description">
        /// If true, adds the Service Control scope. Defaults to true.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public bool ServiceControl { get; set; } = true;

        /// <summary>
        /// <para type="description">
        /// If true, adds the Service Management scope. Defaults to true.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public bool ServiceManagement { get; set; } = true;

        /// <summary>
        /// <para type="description">
        /// The type of Storage scope to add. Defaults to Read. Also accepts None, Write, ReadWrite and Full.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public ReadWrite Storage { get; set; } = ReadWrite.Read;

        /// <summary>
        /// <para type="description">
        /// If set, adds the Task queue scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter TaskQueue { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, adds the User info scope.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.FromFlags)]
        public SwitchParameter UserInfo { get; set; }

        /// <summary>
        /// Used to collect scopes from the pipeline to be used in EndProcessing.
        /// </summary>
        private readonly List<string> _scopeUris = new List<string>();

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.FromScopeUris:
                    _scopeUris.AddRange(ScopeUri);
                    break;
                case ParameterSetNames.FromFlags:
                    WriteObject(BuildFromFlags());
                    break;
                default:
                    throw new PSInvalidOperationException($"{ParameterSetName} is not a valid parameter set");
            }
        }

        /// <summary>
        /// If there are collected scopes, create a new service account from them.
        /// </summary>
        protected override void EndProcessing()
        {
            if (_scopeUris.Count > 0)
            {
                WriteObject(new ServiceAccount
                {
                    Email = Email,
                    Scopes = _scopeUris
                });
            }
            base.EndProcessing();
        }

        /// <summary>
        /// Creates a ServiceAccount object from the email and uses the given flags to add scopes.
        /// </summary>
        /// <returns></returns>
        private ServiceAccount BuildFromFlags()
        {
            var serviceAccount = new ServiceAccount
            {
                Email = Email,
                Scopes = new List<string>()
            };
            const string baseUri = "https://www.googleapis.com/auth/";

            if (BigQuery)
            {
                serviceAccount.Scopes.Add($"{baseUri}bigquery");
            }

            switch (BigtableAdmin)
            {
                case BigTableAdminEnum.Tables:
                    serviceAccount.Scopes.Add($"{baseUri}bigtable.admin.table");
                    break;
                case BigTableAdminEnum.Full:
                    serviceAccount.Scopes.Add($"{baseUri}bigtable.admin");
                    break;
            }

            switch (BigtableData)
            {
                case ReadWrite.Read:
                    serviceAccount.Scopes.Add($"{baseUri}bigtable.data.readonly");
                    break;
                case ReadWrite.ReadWrite:
                    serviceAccount.Scopes.Add($"{baseUri}bigtable.data");
                    break;
            }

            if (CloudDatastore)
            {
                serviceAccount.Scopes.Add($"{baseUri}datastore");
            }

            switch (CloudLogging)
            {
                case ReadWrite.Write:
                    serviceAccount.Scopes.Add($"{baseUri}logging.write");
                    break;
                case ReadWrite.Read:
                    serviceAccount.Scopes.Add($"{baseUri}logging.read");
                    break;
                case ReadWrite.Full:
                    serviceAccount.Scopes.Add($"{baseUri}logging.admin");
                    break;
            }

            switch (CloudMonitoring)
            {
                case ReadWrite.Write:
                    serviceAccount.Scopes.Add($"{baseUri}monitoring.write");
                    break;
                case ReadWrite.Read:
                    serviceAccount.Scopes.Add($"{baseUri}monitoring.read");
                    break;
                case ReadWrite.Full:
                    serviceAccount.Scopes.Add($"{baseUri}monitoring");
                    break;
            }

            if (CloudPubSub)
            {
                serviceAccount.Scopes.Add($"{baseUri}pubsub");
            }

            if (CloudSQL)
            {
                serviceAccount.Scopes.Add($"{baseUri}sqlservice.admin");
            }

            switch (Compute)
            {
                case ReadWrite.Read:
                    serviceAccount.Scopes.Add($"{baseUri}compute.readonly");
                    break;
                case ReadWrite.ReadWrite:
                    serviceAccount.Scopes.Add($"{baseUri}compute");
                    break;
            }

            if (ServiceControl)
            {
                serviceAccount.Scopes.Add($"{baseUri}servicecontrol");
            }

            if (ServiceManagement)
            {
                serviceAccount.Scopes.Add($"{baseUri}service.management");
            }

            switch (Storage)
            {
                case ReadWrite.Read:
                    serviceAccount.Scopes.Add($"{baseUri}devstorage.read_only");
                    break;
                case ReadWrite.Write:
                    serviceAccount.Scopes.Add($"{baseUri}devstorage.write_only");
                    break;
                case ReadWrite.ReadWrite:
                    serviceAccount.Scopes.Add($"{baseUri}devstorage.read_write");
                    break;
                case ReadWrite.Full:
                    serviceAccount.Scopes.Add($"{baseUri}devstorage.full_control");
                    break;
            }

            if (TaskQueue)
            {
                serviceAccount.Scopes.Add($"{baseUri}taskqueue");
            }

            if (UserInfo)
            {
                serviceAccount.Scopes.Add($"{baseUri}userinfo.email");
            }

            return serviceAccount;
        }
    }
}