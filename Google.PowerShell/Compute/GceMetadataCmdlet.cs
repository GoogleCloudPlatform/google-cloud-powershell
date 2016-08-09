// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Net;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets the current metadata from the metadata server.
    /// </para>
    /// <para type="description">
    /// Gets the current metadata from the metadata server. Get-GceMetadata can only be called from a Google
    /// Compute Engine VM instance. Calls from any other machine will fail.
    /// </para>
    /// <example>
    /// <code>PS C:\> $allMetadata = Get-GceMetadata | ConvertFrom-Json</code>
    /// <para>Gets all of the metadata and converts it from the given json to a PSCustomObject.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> $hostName = Get-GceMetadata -Path "instance/hostname" -NotRecursive</code>
    /// <para>Gets the hostname of the instance.</para>
    /// </example>
    /// <example>
    /// <code>
    ///     PS C:\> $newTags, $newEtag = Get-GceMetadata -Path "instance/tags" -AppendEtag -WaitUpdate `
    ///         -LastETag $oldETag
    /// </code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceMetadata")]
    [OutputType(typeof(string))]
    public class GetGceMetadataCmdlet : GCloudCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The path to the specific metadata you wish to get e.g. "instance/tags", "instance/attributes",
        /// "project/attributes/sshKeys".
        /// </para>
        /// </summary>
        [Parameter]
        public string Path { get; set; } = "";

        /// <summary>
        /// <para type="description">
        /// If set, will only get the direct children of the current path.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter NotRecursive { get; set; }

        /// <summary>
        /// <para type="description">
        /// When set, the value of the respone etag will be appended to the output pipeline after the content.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter AppendETag { get; set; }

        /// <summary>
        /// <para type="description">
        /// If true, the query will wait for the metadata to update.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter WaitUpdate { get; set; }

        /// <summary>
        /// <para type="description">
        /// The last etag known. Used in conjunction with -WaitUpdate. If the last etag does not match the
        /// current etag of the metadata server, it will return immediatly.
        /// </para>
        /// </summary>
        [Parameter]
        public string LastETag { get; set; }

        /// <summary>
        /// <para type="description">
        /// Used in conjunction with -WaitUpdate. The amout of time to wait before returning.
        /// </para>
        /// </summary>
        [Parameter]
        public TimeSpan? Timeout { get; set; }

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
                if (LastETag != null)
                {
                    queryParameters.Add($"last_etag={LastETag}");
                }
                if (Timeout != null)
                {
                    queryParameters.Add($"timeout_sec={(int)Timeout.Value.TotalSeconds}");
                }
            }

            string query = string.Join("&", queryParameters);
            HttpWebRequest request = WebRequest.CreateHttp($"{basePath}{Path}?{query}");
            request.Headers.Add("Metadata-Flavor:Google");
            if (WaitUpdate)
            {
                request.Timeout = -1;
            }

            using (WebResponse response = request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader streamReader = new StreamReader(responseStream))
            {
                WriteObject(streamReader.ReadToEnd());
                if (AppendETag)
                {
                    WriteObject(response.Headers["ETag"]);
                }
            }
        }
    }
}
