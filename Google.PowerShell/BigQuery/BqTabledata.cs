// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Net;
using System.Management.Automation;
using System.Collections.Generic;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// <para type="synopsis">
    /// Instantiates a new BQ schema or adds a field to a pre-existing schema.
    /// </para>
    /// <para type="description">
    /// If no existing schema is passed in, this command will create a new schema with one column. 
    /// If an existing schema is supplied, this command will add a new column to that schema. 
    /// Required fields for each column are Name and Type. Possible values for Type include STRING, 
    /// BYTES, INTEGER (also called INT64), FLOAT (also FLOAT64), BOOLEAN (also BOOL), TIMESTAMP, 
    /// DATE, TIME, DATETIME, and RECORD (where RECORD indicates that the field contains a nested 
    /// schema. Also called STRUCT). Possible values for Mode include NULLABLE, REQUIRED, and 
    /// REPEATED. Case is ignored for both Type and Mode. This command returns the new or modified 
    /// TableSchema object.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $table = Get-BqDataset “book_data” | Get-BqTable "21st_century"
    /// PS C:\> $schema = New-BqSchema -Name "Title" -Type "STRING" -Description "Book Title"
    /// PS C:\> $schema = $schema | New-BqSchema -Name "Author" -Type "STRING" -Description "Book Author"
    /// PS C:\> $table.Schema = $schema
    /// PS C:\> $table | Set-BqTable
    ///   </code>
    ///   <para>This will create a new schema, assign it to a table, and then send the 
    ///   revised table to the server to be saved.</para>
    /// </example> 
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "BqSchema")]
    public class NewBqSchema : PSCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The existing TableSchema that you wish to add a column to. 
        /// If this value is not present, a new schema will be created.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipeline = true)]
        public TableSchema InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the column to be added. The name must be unique among columns in each schema.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of the column to be added. Possible values include STRING, BYTES, INTEGER (INT64), 
        /// FLOAT (FLOAT64), BOOLEAN (BOOL), TIMESTAMP, DATE, TIME, DATETIME, and RECORD (STRUCT).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateSet("STRING", "BYTES", "INTEGER", "INT64", "FLOAT", "FLOAT64", "BOOLEAN", "BOOL", 
            "TIMESTAMP", "DATE", "TIME", "DATETIME", "RECORD", "STRUCT")]
        public string Type { get; set; }

        /// <summary>
        /// <para type="description">
        /// An optional description for this column.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The mode of the column to be added. Possible values include NULLABLE, REQUIRED, and REPEATED.
        /// The default value is NULLABLE.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateSet("NULLABLE", "REQUIRED", "REPEATED", IgnoreCase = true)]
        public string Mode { get; set; }

        /// <summary>
        /// <para type="description">
        /// Describes the optional nested schema fields if the type property is set to RECORD. Pass in 
        /// another TableSchema object and this cmdlet will properly nest its fields in the new column.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public TableSchema Fields { get; set; }

        protected override void ProcessRecord()
        {
            if (InputObject == null)
            {
                InputObject = new TableSchema();
                InputObject.Fields = new List<TableFieldSchema>();
            }

            if (Mode == null)
            {
                Mode = "NULLABLE";
            }

            foreach (TableFieldSchema t in InputObject.Fields)
            {
                if (Name.Equals(t.Name))
                {
                    WriteError(new ErrorRecord(
                        new Exception($"This schema already contains a column with name '{Name}'."),
                        "Column Name Collision", ErrorCategory.InvalidArgument, Name));
                }
            }

            TableFieldSchema tfs = new TableFieldSchema();
            tfs.Name = Name;
            tfs.Type = Type;
            tfs.Description = (Description != null) ? Description : null;
            tfs.Mode = (Mode != null) ? Mode : null;
            tfs.Fields = (Fields != null) ? Fields.Fields : null;

            InputObject.Fields.Add(tfs);
            WriteObject(InputObject);
        }
    }
}
