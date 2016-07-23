// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.PowerShell.Common;
using Google.Apis.SQLAdmin.v1beta4;
using System.Management.Automation;
using System.Diagnostics;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Services;
using Google.Apis.SQLAdmin.v1beta4.Data;

namespace Google.PowerShell.Sql
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists all available database flags for Google Cloud SQL instances.
    /// </para>
    /// <para type="description">
    /// Lists all available database flags for instances.
    /// </para>
    /// <example>
    ///   <para>
    ///   Gets a list of database flags available for instances.
    ///   </para>
    ///   <para><code>
    ///     PS C:\> Get-GcSqlFlags
    ///   </code></para>
    ///   <br></br>
    ///   <para>(If successful, the command returns a list of database flags available for instances)</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcSqlFlags")]
    [OutputType(typeof(IList<Flag>))]
    public class GetGcSqlFlagsCmdlet : GcSqlCmdlet
    {
        protected override void ProcessRecord()
        {
            FlagsResource.ListRequest flags = Service.Flags.List();
            FlagsListResponse response = flags.Execute();
            WriteObject(response.Items, true);
        }
    }
}
