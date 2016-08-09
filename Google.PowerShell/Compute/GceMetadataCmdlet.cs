// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Net;

namespace Google.PowerShell.ComputeEngine
{
    [Cmdlet(VerbsCommon.Get, "GceMetadata")]
    public class GetGceMetadataCmdlet : GCloudCmdlet
    {
        [Parameter]
        public string Path { get; set; } = "";

        [Parameter]
        public SwitchParameter WaitUpdate { get; set; }

        [Parameter]
        public string ETag { get; set; }

        [Parameter]
        public TimeSpan? Timeout { get; set; }

        [Parameter]
        public SwitchParameter NotRecursive { get; set; }

        protected override void ProcessRecord()
        {
            const string basePath = "http://metadata.google.internal/computeMetadata/v1/";
            var queryParameters = new List<string>();
            if (!NotRecursive)
            {
                queryParameters.Add("recursive=true");
            }
            if (WaitUpdate)
            {
                queryParameters.Add("wait_for_change=true");
                if (ETag != null)
                {
                    queryParameters.Add($"last_etag={ETag}");
                }
                if (Timeout != null)
                {
                    queryParameters.Add($"timeout_set={(int)Timeout.Value.TotalSeconds}");
                }
            }

            string query = string.Join("&", queryParameters);
            HttpWebRequest request = WebRequest.CreateHttp($"{basePath}{Path}?{query}");
            request.Headers.Add("Metadata-Flavor: Google");

            using (WebResponse response = request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader streamReader = new StreamReader(responseStream))
            {
                WriteObject(JsonConvert.DeserializeObject(streamReader.ReadToEnd()));
            }
        }
    }
}
