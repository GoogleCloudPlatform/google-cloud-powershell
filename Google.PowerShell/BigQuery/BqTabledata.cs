// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Cloud.BigQuery.V2;
using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;
using Google.PowerShell.Common;
using System;
using System.Net;
using System.Linq;
using System.Management.Automation;
using System.Collections.Generic;
using System.IO;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// Possible types for each column in a TableSchema
    /// </summary>
    public enum ColumnType
    {
        STRING, BYTES, INTEGER, INT64,
        FLOAT, FLOAT64, BOOLEAN, BOOL,
        TIMESTAMP, DATE, TIME, DATETIME,
        RECORD, STRUCT
    }

    /// <summary>
    /// Possible types for each column in a TableSchema
    /// </summary>
    public enum ColumnMode
    {
        NULLABLE, REQUIRED, REPEATED
    }

    /// <summary>
    /// <para type="synopsis">
    /// Instantiates a new BQ schema or adds a field to a pre-existing schema.
    /// </para>
    /// <para type="description">
    /// This command defines one column of a TableSchema. To create a multi-row schema, chain 
    /// multiple instances of this command together on the pipeline. Required fields for each 
    /// column are Name and Type. Possible values for Type include STRING, BYTES, INTEGER, 
    /// FLOAT, BOOLEAN, TIMESTAMP, DATE, TIME, DATETIME, and RECORD (where RECORD indicates 
    /// that the field contains a nested schema). Case is ignored for both Type and Mode. 
    /// Possible values for the Mode field include REQUIRED, REPEATED, and the default NULLABLE. 
    /// This command forwards all TableFieldSchemas that it is passed, and will add a new 
    /// TableFieldSchema object to the pipeline.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $table = $dataset_books | New-BqTable "book_info"
    /// PS C:\> $result = New-BqSchema -Name "Author" -Type "STRING" | `
    ///   New-BqSchema -Name "Copyright" -Type "STRING" | `
    ///   New-BqSchema -Name "Title" -Type "STRING" | `
    ///   Set-BqSchema $table
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
        /// Holder parameter to allow cmdlet to forward TableFieldSchemas down the pipeline.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipeline = true)]
        public TableFieldSchema PassThruObject { get; set; }

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
        public ColumnType Type { get; set; }

        /// <summary>
        /// <para type="description">
        /// An optional description for this column.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The mode of the column to be added. Possible values include NULLABLE, REQUIRED, and REPEATED.
        /// The default value is NULLABLE.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public ColumnMode? Mode { get; set; }

        /// <summary>
        /// <para type="description">
        /// Describes the optional nested schema fields if the type property is set to RECORD. Pass in 
        /// an array of TableFieldSchema objects and it will be nested inside a single column.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public TableFieldSchema[] Fields { get; set; }

        protected override void ProcessRecord()
        {
            if (PassThruObject != null)
            {
                WriteObject(PassThruObject);
            }
        }

        protected override void EndProcessing()
        {
            if (Mode == null)
            {
                Mode = ColumnMode.NULLABLE;
            }

            TableFieldSchema tfs = new TableFieldSchema();
            tfs.Name = Name;
            tfs.Type = Type.ToString();
            tfs.Description = Description;
            tfs.Mode = Mode.ToString();
            tfs.Fields = Fields;
            WriteObject(tfs);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Attaches a TableSchema to a BQ Table.
    /// </para>
    /// <para type="description">
    /// This command takes a Table and sets its schema to be the aggregation of all TableFieldSchema 
    /// objects passed in. If multiple columns are passed in with the same “Name” field, an error 
    /// will be thrown. This command returns the modified Table object after updating the cloud resource.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $table = Get-BqDataset “book_data” | Get-BqTable "21st_century"
    /// PS C:\> $table = New-BqSchema -Name “Title” -Type “STRING” | Set-BqSchema $table
    ///   </code>
    ///   <para>This will create a new schema, assign it to a table, and then send the 
    ///   revised table to the server to be saved.</para>
    /// </example> 
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "BqSchema")]
    public class SetBqSchema : BqCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Variable to aggregate the TableFieldSchemas from the pipeline.  Pipe one or 
        /// TableFieldSchema object in using the New-BqSchema cmdlet.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNull]
        public TableFieldSchema InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The table that you wish to add this schema to.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNull]
        public Table Table { get; set; }

        List<TableFieldSchema> Columns = new List<TableFieldSchema>();

        protected override void ProcessRecord()
        {
            if (Columns.Any(field => InputObject.Name.Equals(field.Name)))
            {
                // ATTENTION:  should this throw? (to notify users that data may have been lost)
                // Or would it be sufficient to document only keeping the first one seen as 
                // deterministic behavior for this method?
                ThrowTerminatingError(new ErrorRecord(
                    new Exception($"This schema already contains a column with name '{InputObject.Name}'."),
                    "Column Name Collision", ErrorCategory.InvalidArgument, InputObject.Name));
            }
            Columns.Add(InputObject);
        }

        protected override void EndProcessing()
        {
            Table.Schema = new TableSchema();
            Table.Schema.Fields = Columns;
            Table.ETag = "";
            var request = Service.Tables.Update(Table,
                Table.TableReference.ProjectId,
                Table.TableReference.DatasetId,
                Table.TableReference.TableId);
            try
            {
                Table response = request.Execute();
                WriteObject(response);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
            {
                WriteError(new ErrorRecord(ex,
                    $"Conflict while updating '{Table.TableReference.DatasetId}'.",
                    ErrorCategory.WriteError,
                    Table));
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
            {
                WriteError(new ErrorRecord(ex,
                    $"You do not have permission to modify '{Table.TableReference.DatasetId}'.",
                    ErrorCategory.PermissionDenied,
                    Table));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Streams data from a file into BigQuery one record at a time without needing to run a load job.
    /// </para>
    /// <para type="description">
    /// Streams data into BigQuery one record at a time without needing to run a load job. This cmdlet 
    /// accepts CSV, JSON, and Avro files, and has a number of configuration parameters for each type. 
    /// This cmdlet returns nothing if the insert completed successfully.
    /// WriteMode Options:
    /// - WriteAppend will add data on to the existing table.
    /// - WriteTruncate will truncate the table before additional data is inserted.
    /// - WriteIfEmpty will throw an error unless the table is empty.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $filename = "C:\data.json"
    /// PS C:\> $table = New-BqTable -DatasetId "db_name" "tab_name"
    /// PS C:\> $table | Add-BqTabledata $filename JSON
    ///   </code>
    ///   <para>This code will ingest a newline-delimited JSON file from the location $filename on local 
    ///   disk to db_name:tab_name in BigQuery.</para>
    ///   <code>
    /// PS C:\> $filename = "C:\data.csv"
    /// PS C:\> $table = New-BqTable -DatasetId "db_name" "tab_name"
    /// PS C:\> $table | Add-BqTabledata $filename CSV -SkipLeadingRows 1 `
    ///     -MaxBadRecords 4 -AllowUnknownFields
    ///   </code>
    ///   <para>This code will take a CSV file and upload it to a BQ table.  It will ignore up to 4 bad 
    ///   rows before throwing an error, and it will keep rows that have fields that aren't in the 
    ///   table's schema.</para>
    /// </example> 
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tabledata)">
    /// [BigQuery Tabledata]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "BqTabledata")]
    public class AddBqTabledata : BqCmdlet
    {
        /// <summary>
        /// Data format of the file being passed in.
        /// </summary>
        public enum DataFormats
        {
            AVRO, CSV, JSON
        }

        /// <summary>
        /// <para type="description">
        /// The table to insert the data.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [ValidateNotNull]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Table), Property = nameof(Table.TableReference))]
        public TableReference InputObject { get; set; }

        /// <summary>
        /// <para type="description">
        /// The filname containing the data to insert.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Filename { get; set; }

        /// <summary>
        /// <para type="description">
        /// The format of the data file (Ex: CSV, JSON).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public DataFormats Type { get; set; }

        /// <summary>
        /// <para type="description">
        /// Write Disposition of the operation. Governs what happens to the data currently in the table.
        /// If this parameter is not supplied, this defaults to WriteAppend
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public WriteDisposition WriteMode { get; set; }

        /// <summary>
        /// <para type="description">
        /// The number of malformed rows that the request will ignore before throwing an error. 
        /// This value is zero if not specified.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public int MaxBadRecords { get; set; }

        /// <summary>
        /// <para type="description">
        /// CSV ONLY: Allows insertion of rows with fields that are not in the schema, ignoring the extra fields.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter AllowUnknownFields { get; set; }

        /// <summary>
        /// <para type="description">
        /// CSV ONLY: Allows insertion of rows that are missing trailing optional columns.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter AllowJaggedRows { get; set; }

        /// <summary>
        /// <para type="description">
        /// CSV ONLY: Allows quoted data sections to contain newlines
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter AllowQuotedNewlines { get; set; }

        /// <summary>
        /// <para type="description">
        /// CSV ONLY: Separator between fields in the data.  Default value is comma (,).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string FieldDelimiter { get; set; }

        /// <summary>
        /// <para type="description">
        /// CSV ONLY: Value used to quote data sections.  Default value is double-quote (").
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string Quote { get; set; }

        /// <summary>
        /// <para type="description">
        /// CSV ONLY: The number of rows to skip from the input file. (Usually used for headers.)
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public int SkipLeadingRows { get; set; }

        protected override void ProcessRecord()
        {
            var Client = BigQueryClient.Create(InputObject.ProjectId);

            try
            {
                using (Stream fileInput = File.OpenRead(Filename))
                {
                    BigQueryJob bqj;

                    switch (Type)
                    {
                        case DataFormats.AVRO:
                            UploadAvroOptions AvroOptions = new UploadAvroOptions();
                            AvroOptions.WriteDisposition = WriteMode;
                            AvroOptions.MaxBadRecords = MaxBadRecords;
                            AvroOptions.AllowUnknownFields = AllowUnknownFields;
                            bqj = Client.UploadAvro(InputObject, null, fileInput, AvroOptions);
                            break;
                        case DataFormats.JSON:
                            UploadJsonOptions JsonOptions = new UploadJsonOptions();
                            JsonOptions.WriteDisposition = WriteMode;
                            JsonOptions.MaxBadRecords = MaxBadRecords;
                            JsonOptions.AllowUnknownFields = AllowUnknownFields;
                            bqj = Client.UploadJson(InputObject, null, fileInput, JsonOptions);
                            break;
                        case DataFormats.CSV:
                            UploadCsvOptions CsvOptions = new UploadCsvOptions();
                            CsvOptions.WriteDisposition = WriteMode;
                            CsvOptions.MaxBadRecords = MaxBadRecords;
                            CsvOptions.AllowJaggedRows = AllowJaggedRows;
                            CsvOptions.AllowQuotedNewlines = AllowQuotedNewlines;
                            CsvOptions.AllowTrailingColumns = AllowUnknownFields;
                            CsvOptions.FieldDelimiter = FieldDelimiter;
                            CsvOptions.Quote = Quote;
                            CsvOptions.SkipLeadingRows = SkipLeadingRows;
                            bqj = Client.UploadCsv(InputObject, null, fileInput, CsvOptions);
                            break;
                        default:
                            throw UnknownParameterSetException;
                    }

                    bqj.PollUntilCompleted().ThrowOnAnyError();
                }
            }
            catch (IOException ex)
            {
                WriteError(new ErrorRecord(ex,
                    $"Error while reading file '{Filename}'.",
                    ErrorCategory.ReadError, Filename));
                return;
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex,
                    $"Error while uploading file '{Filename}' to table '{InputObject.TableId}'.",
                    ErrorCategory.WriteError, Filename));
            }
        }
    }
}
