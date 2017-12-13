// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Bigquery.v2;
using Google.Cloud.BigQuery.V2;
using Google.PowerShell.Common;
using System;

namespace Google.PowerShell.BigQuery
{
    /// <summary>
    /// Base class for Google Cloud BigQuery cmdlets.
    /// </summary>
    public class BqCmdlet : GCloudCmdlet
    {
        private readonly Lazy<BigqueryService> _service;
        private readonly Lazy<BigQueryClient> _client;

        public BigqueryService Service => _service.Value;
        public BigQueryClient Client => _client.Value;

        internal static BigqueryService OptionalBigQueryService { private get; set; }
        internal static BigQueryClient OptionalBigQueryClient { private get; set; }

        public BqCmdlet()
        {
            _service = new Lazy<BigqueryService>(() => OptionalBigQueryService ??
                new BigqueryService(GetBaseClientServiceInitializer()));
            _client = new Lazy<BigQueryClient>(() => OptionalBigQueryClient ??
                BigQueryClient.Create(Project));
        }

        // String value of DataFormats.JSON that is taken by the rest API.
        public static string JSON_TEXT = "NEWLINE_DELIMITED_JSON";
        public static string COMPRESSION_GZIP = "GZIP";
        public static string COMPRESSION_NONE = "NONE";
        public static string STATUS_DONE = "DONE";

        // String value for Google.Apis.Requests.RequestError class to signal Database not found (404).
        public static string DS_404 = "Not found: Dataset";
        public static string TAB_404 = "Not found: Table";
    }

    /// <summary>
    /// Data formats for input and output
    /// </summary>
    public enum DataFormats
    {
        AVRO,               // Apache AVRO file format (avro.apache.org)
        CSV,                // Comma Separated Value file
        JSON,               // Newline-delimited JSON (ndjson.org)
        DATASTORE_BACKUP    // Cloud Datastore backup files
    }
}
