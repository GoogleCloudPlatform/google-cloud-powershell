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
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/storing-retrieving-metadata)">
    /// [Metadata Documentation]
    /// </para>
    /// <example>
    /// <code>PS C:\> $allMetadata = Get-GceMetadata -Recursive | ConvertFrom-Json</code>
    /// <para>Gets all of the metadata and converts it from the given JSON to a PSCustomObject.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> $hostName = Get-GceMetadata -Path "instance/hostname" </code>
    /// <para>Gets the hostname of the instance.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> $customProjectMetadata = Get-GceMetadata -Path "project/attributes/customKey" </code>
    /// <para>Gets the value of the custom metadata with key "customKey" placed in the project .</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> $metadata, $etag = Get-GceMetadata -AppendETag -Recursive</code>
    /// <para>Gets the entire metadata tree, and the ETag of the version retrieved.</para>
    /// </example>
    /// <example>
    /// <code>
    ///     PS C:\> $newTags, $newEtag = Get-GceMetadata -Path "instance/tags" -AppendETag -WaitUpdate `
    ///                                     -LastETag $oldETag
    /// </code>
    /// <para>Waits for the metadata "instance/tags" to be updated by the server.</para>
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
        public string Path { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will get the metadata subtree as a JSON string. If -Path is not set, will get the entire
        /// metadata tree as a JSON string.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Recursive { get; set; }

        /// <summary>
        /// <para type="description">
        ///   When set, the value of the respone ETag will be appended to the output pipeline after the content.
        /// </para>
        /// <para type="description">
        /// <code>PS C:\> $metadata, $etag = Get-GceMetadata -AppendETag -Recursive</code>
        /// </para>
        /// <para type="description">
        ///   Gets the entire metadata tree, and the ETag of the version retrieved.
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
        /// The last ETag known. Used in conjunction with -WaitUpdate. If the last ETag does not match the
        /// current ETag of the metadata server, it will return immediatly.
        /// </para>
        /// </summary>
        [Parameter]
        public string LastETag { get; set; }

        /// <summary>
        /// <para type="description">
        /// Used in conjunction with -WaitUpdate. The amout of time to wait. If the timeout expires, the 
        /// current metadata will be returned.
        /// </para>
        /// </summary>
        [Parameter]
        public TimeSpan? Timeout { get; set; }

        protected override void ProcessRecord()
        {
            const string basePath = "http://metadata.google.internal/computeMetadata/v1/";
            var queryParameters = new List<string>();
            if (Recursive)
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
            try
            {
                using (WebResponse response = request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader streamReader = new StreamReader(responseStream ?? Stream.Null))
                {
                    WriteObject(streamReader.ReadToEnd());
                    if (AppendETag)
                    {
                        WriteObject(response.Headers["ETag"]);
                    }
                }
            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    throw new Exception(
                        "Get-GceMetadata can only be used in a Google Compute VM instance.", e);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
