// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.SQLAdmin.v1beta4;
using Google.Apis.SQLAdmin.v1beta4.Data;
using System.Management.Automation;
using Google.PowerShell.Common;
using System.Collections.Generic;

namespace Google.PowerShell.Sql
{
    /// <summary>
    /// <para type="synopsis">
    /// Retrieves a particular SSL certificate, or lists the current SSL certificates for an instance.
    /// Does not include the private key- for the private key must be saved from the response to initial creation.
    /// </para>
    /// <para type="description">
    /// Retrieves the specified SSL certificate, or lists the current SSL certificates for that instance.
    /// This is determined by if an Sha1Fingerprint is specified or not.
    /// Does not include the private key.
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlSslCert")]
    public class GetGcSqlSslCmdlet : GcSqlCmdlet
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
        /// Sha1 FingerPrint for the SSL Certificate.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.GetSingle)]
        public string Sha1Fingerprint { get; set; }

        protected override void ProcessRecord()
        {
            if (Sha1Fingerprint != null)
            {
                SslCertsResource.GetRequest request = Service.SslCerts.Get(Project, Instance, Sha1Fingerprint);
                SslCert result = request.Execute();
                WriteObject(result);
            }
            else
            {
                SslCertsResource.ListRequest request = Service.SslCerts.List(Project, Instance);
                SslCertsListResponse result = request.Execute();
                WriteObject(result.Items, true);
            }
        }
    }
}
