// Copyright 2015-2017 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Cloud.BigQuery.V2;
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
    /// This command defines one column of a TableSchema. To create a multi-row schema, either chain 
    /// multiple instances of this command together on the pipeline, pass in a JSON array that describes 
    /// the schema as a string with "-JSON", or pass in a file containing the JSON array with "-Filename".
    /// Required fields for each column are Name and Type. Possible values for Type include "STRING", 
    /// "BYTES", "INTEGER", "FLOAT", "BOOLEAN", "TIMESTAMP", "DATE", "TIME", "DATETIME", and "RECORD" 
    /// ("RECORD" indicates the field contains a nested schema). Case is ignored for both Type and Mode. 
    /// Possible values for the Mode field include "REQUIRED", "REPEATED", and the default "NULLABLE". 
    /// This command forwards all TableFieldSchemas that it is passed, and will add one or more new 
    /// TableFieldSchema objects to the pipeline.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $dataset = New-BqDataset "books"
    /// PS C:\> $table = $dataset | New-BqTable "book_info"
    /// PS C:\> $result = New-BqSchema "Author" "STRING" | New-BqSchema "Copyright" "STRING" |
    ///                   New-BqSchema "Title" "STRING" | Set-BqSchema $table
    ///   </code>
    ///   <para>This will create a new schema, assign it to a table, and then send the 
    ///   revised table to the server to be saved.</para>
    /// </example> 
    /// <example>
    ///   <code>
    /// PS C:\> $dataset = New-BqDataset "books"
    /// PS C:\> $table = $dataset | New-BqTable "book_info"
    /// PS C:\> $result = New-BqSchema -JSON `
    ///                   '[{"Name":"Title","Type":"STRING"},{"Name":"Author","Type":"STRING"},{"Name":"Year","Type":"INTEGER"}]' |
    ///                   Set-BqSchema $table
    ///   </code>
    ///   <para>This will create a new schema using JSON input and will assign it to a table.</para>
    /// </example> 
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "BqSchema")]
    public class NewBqSchema : PSCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByValue = "ByValue";
            public const string ByString = "ByString";
            public const string ByFile = "ByFile";
        }

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
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByValue)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The type of the column to be added. Possible values include "STRING", "BYTES", "INTEGER" 
        /// (INT64), "FLOAT" (FLOAT64), "BOOLEAN" (BOOL), "TIMESTAMP", "DATE", "TIME", "DATETIME", 
        /// and "RECORD" (STRUCT).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.ByValue)]
        public ColumnType Type { get; set; }

        /// <summary>
        /// <para type="description">
        /// An optional description for this column.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 2, ParameterSetName = ParameterSetNames.ByValue)]
        [ValidateNotNull]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">
        /// The mode of the column to be added. Possible values include "NULLABLE", "REQUIRED", and 
        /// "REPEATED". The default value is "NULLABLE".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, Position = 3, ParameterSetName = ParameterSetNames.ByValue)]
        public ColumnMode? Mode { get; set; } = ColumnMode.NULLABLE;

        /// <summary>
        /// <para type="description">
        /// Describes the optional nested schema fields if the type property is set to "RECORD". Pass in 
        /// an array of TableFieldSchema objects and it will be nested inside a single column.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByValue)]
        [ValidateNotNullOrEmpty]
        public TableFieldSchema[] Fields { get; set; }

        /// <summary>
        /// <para type="description">
        /// JSON string of the schema. Should be in the form: 
        /// [{"Name":"Title","Type":"STRING"},{"Name":"Author","Type":"STRING"},{"Name":"Year","Type":"INTEGER"}]
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByString)]
        [ValidateNotNull]
        public string JSON { get; set; }

        /// <summary>
        /// <para type="description">
        /// File to read a JSON schema from.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByFile)]
        [ValidateNotNull]
        public string Filename { get; set; }

        protected override void ProcessRecord()
        {
            if (PassThruObject != null)
            {
                WriteObject(PassThruObject);
            }
        }

        protected override void EndProcessing()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByValue:
                    TableFieldSchema tfs = new TableFieldSchema()
                    {
                        Name = Name,
                        Type = Type.ToString(),
                        Description = Description,
                        Mode = Mode.ToString(),
                        Fields = Fields
                    };
                    WriteObject(tfs);
                    break;
                case ParameterSetNames.ByString:
                    WriteObject(JsonToColumns(JSON), true);
                    break;
                case ParameterSetNames.ByFile:
                    WriteObject(JsonToColumns(File.ReadAllText(Filename)), true);
                    break;
                default:
                    throw new Exception("Invalid parameter set in NewBqSchema");
            }
        }

        /// <summary>
        /// Helper method to serialize TableFieldSchema records.
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public TableFieldSchema[] JsonToColumns(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<TableFieldSchema[]>(json);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Attaches a TableSchema to a BQ Table.
    /// </para>
    /// <para type="description">
    /// This command takes a Table and sets its schema to be the aggregation of all TableFieldSchema 
    /// objects passed in from New-BqSchema calls earlier on the pipeline. If multiple columns are 
    /// passed in with the same "-Name" field, an error will be thrown. If no Table argument is passed in, 
    /// the Schema object will be written to the pipeline and the cmdlet will quit. This can be used in 
    /// combination with the -Schema flag in New-BqTable to apply one schema to multiple tables. If a 
    /// Table is passed in, this command returns a Table object showing the updated server state.
    /// <example>
    ///   <code>
    /// PS C:\> $table = Get-BqTable "21st_century" -DatasetId "book_data"
    /// PS C:\> $table = New-BqSchema "Title" "STRING" | Set-BqSchema $table
    ///   </code>
    ///   <para>This will create a new schema, assign it to a table, and then send the 
    ///   revised table to the server to be saved.</para>
    /// </example>
    /// <example>
    ///   <code>
    /// PS C:\> $schema = New-BqSchema "Title" "STRING" | New-BqSchema "Author" "STRING" | Set-BqSchema
    /// PS C:\> $table1 = New-BqTable "my_table" -DatasetId "my_dataset" -Schema $schema
    /// PS C:\> $table2 = New-BqTable "another_table" -DatasetId "my_dataset" -Schema $schema
    ///   </code>
    ///   <para>This will create a new schema and save it to a variable so it can be passed into 
    ///   multiple table creation cmdlets.</para>
    /// </example> 
    /// </para>
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tables)">
    /// [BigQuery Tables]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "BqSchema")]
    public class SetBqSchema : BqCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// Variable to aggregate the TableFieldSchemas from the pipeline. Pipe one or 
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
        [Parameter(Mandatory = false, Position = 0)]
        public Table Table { get; set; }

        List<TableFieldSchema> Columns = new List<TableFieldSchema>();

        protected override void ProcessRecord()
        {
            if (Columns.Any(field => InputObject.Name.Equals(field.Name)))
            {
                ThrowTerminatingError(new ErrorRecord(
                    new Exception($"This schema already contains a column with name '{InputObject.Name}'."),
                    "Column Name Collision", ErrorCategory.InvalidArgument, InputObject.Name));
            }
            Columns.Add(InputObject);
        }

        protected override void EndProcessing()
        {
            // Check if the user just wants a Schema object
            if (Table == null)
            {
                WriteObject(new TableSchema
                {
                    Fields = Columns
                });
                return;
            }

            // Otherwise, update the serverside resource
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
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Conflict while updating '{Table.TableReference.DatasetId}'.",
                    ErrorCategory.WriteError,
                    Table));
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
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
    /// - "WriteAppend" will add data to the existing table.
    /// - "WriteTruncate" will truncate the table before additional data is inserted.
    /// - "WriteIfEmpty" will throw an error unless the table is empty.
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $filename = "C:\data.json"
    /// PS C:\> $table = New-BqTable "tab_name" -DatasetId "db_name"
    /// PS C:\> $table | Add-BqTableRow JSON $filename 
    ///   </code>
    ///   <para>This code will ingest a newline-delimited JSON file from the location "$filename" on local 
    ///   disk to db_name:tab_name in BigQuery.</para>
    ///   <code>
    /// PS C:\> $filename = "C:\data.csv"
    /// PS C:\> $table = New-BqTable "tab_name" -DatasetId "db_name"
    /// PS C:\> $table | Add-BqTableRow CSV $filename -SkipLeadingRows 1 -AllowJaggedRows -AllowUnknownFields
    ///   </code>
    ///   <para>This code will take a CSV file and upload it to a BQ table. It will set missing fields 
    ///   from the CSV to null, and it will keep rows that have fields that aren't in the table's schema.
    ///   </para>
    /// </example> 
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tabledata)">
    /// [BigQuery Tabledata]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "BqTableRow")]
    public class AddBqTableRow : BqCmdlet
    {
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
        /// The format of the data file (CSV | JSON | AVRO).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public DataFormats Type { get; set; }

        /// <summary>
        /// <para type="description">
        /// The filname containing the data to insert.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string Filename { get; set; }

        /// <summary>
        /// <para type="description">
        /// Write Disposition of the operation. Governs what happens to the data currently in the table.
        /// If this parameter is not supplied, this defaults to "WriteAppend".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public WriteDisposition WriteMode { get; set; }

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
        /// CSV ONLY: Separator between fields in the data. Default value is comma (,).
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public string FieldDelimiter { get; set; }

        /// <summary>
        /// <para type="description">
        /// CSV ONLY: Value used to quote data sections. Default value is double-quote (").
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
            Project = InputObject.ProjectId;
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
                            AvroOptions.AllowUnknownFields = AllowUnknownFields;
                            bqj = Client.UploadAvro(InputObject, null, fileInput, AvroOptions);
                            break;
                        case DataFormats.JSON:
                            UploadJsonOptions JsonOptions = new UploadJsonOptions();
                            JsonOptions.WriteDisposition = WriteMode;
                            JsonOptions.AllowUnknownFields = AllowUnknownFields;
                            bqj = Client.UploadJson(InputObject, null, fileInput, JsonOptions);
                            break;
                        case DataFormats.CSV:
                            UploadCsvOptions CsvOptions = new UploadCsvOptions();
                            CsvOptions.WriteDisposition = WriteMode;
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
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Error while reading file '{Filename}'.",
                    ErrorCategory.ReadError, Filename));
                return;
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Error while uploading file '{Filename}' to table '{InputObject.TableId}'.",
                    ErrorCategory.WriteError, Filename));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Retrieves table data from a specified set of rows.
    /// </para>
    /// <para type="description">
    /// Retrieves table data from a specified set of rows. Requires the "READER" dataset role. 
    /// Rows are returned as Google.Cloud.BigQuery.V2.BigQueryRow objects.
    /// Data can be extracted by indexing by column name. (ex: row["title"] )
    /// </para>
    /// <example>
    ///   <code>
    /// PS C:\> $table = Get-BqTable "classics" -DatasetID "book_data"
    /// PS C:\> $list = $table | Get-BqTableRow
    ///   </code>
    ///   <para>Fetches all of the rows in book_data:classics and exports them to "$list".</para>
    /// </example> 
    /// <para type="link" uri="(https://cloud.google.com/bigquery/docs/reference/rest/v2/tabledata)">
    /// [BigQuery Tabledata]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "BqTableRow")]
    public class GetBqTableRow : BqCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The table to export rows from.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
        [ValidateNotNull]
        [PropertyByTypeTransformation(TypeToTransform = typeof(Table), Property = nameof(Table.TableReference))]
        public TableReference InputObject { get; set; }

        protected override void ProcessRecord()
        {
            Project = InputObject.ProjectId;
            try
            {
                var response = Client.ListRows(InputObject, null,
                new ListRowsOptions());

                if (response == null)
                {
                    throw new Exception("Response came back empty (null).");
                }

                WriteObject(response, true);
            }
            catch (Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex,
                    $"Error while exporting rows from table '{InputObject.TableId}'.",
                    ErrorCategory.ReadError, InputObject));
            }
        }
    }
}
