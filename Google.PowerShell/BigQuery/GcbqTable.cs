// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Net;
using System.Management.Automation;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// <para type="synopsis">
    /// Lists BigQuery Tables from a specific project and dataset and returns individual table descriptor objects.
    /// </para>
    /// <para type="description">
    /// test
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcbqTable</code>
    ///   <para>This does a thing</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcbqTable")]
    public class GetGcbqTable : GcbqCmdlet
    {
        protected override void ProcessRecord()
        {
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Updates information describing an existing BigQuery table.
    /// </para>
    /// <para type="description">
    /// text
    /// </para>
    /// <example>
    ///   <code>PS C:\> Set-GcbqTable</code>
    ///   <para>This does a thing</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcbqTable")]
    public class SetGcbqTable : GcbqCmdlet
    {
        protected override void ProcessRecord()
        {
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Creates a new empty table in the specified project and dataset.
    /// </para>
    /// <para type="description">
    /// text
    /// </para>
    /// <example>
    ///   <code>PS C:\> New-GcbqTable</code>
    ///   <para>This does a thing</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "GcbqTable")]
    public class NewGcbqTable : GcbqCmdlet
    {
        protected override void ProcessRecord()
        {
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Deletes the specified table.
    /// </para>
    /// <para type="description">
    /// text
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GcbqTable</code>
    ///   <para>This does a thing</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GcbqTable", SupportsShouldProcess = true)]
    public class RemoveGcbqTable : GcbqCmdlet
    {
        protected override void ProcessRecord()
        {
        }
    }
}
