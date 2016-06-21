// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management.Automation;

namespace Google.PowerShell.ComputeEngine
{
    /// <summary>
    /// Library class for transforming tables into metadata.
    /// </summary>
    public class InstanceMetadataPSConverter
    {
        public static Metadata BuildMetadata(IDictionary table)
        {
            var metadata = new Metadata
            {
                Items = new List<Metadata.ItemsData>()
            };

            var e = table.GetEnumerator();
            while (e.MoveNext())
            {
                metadata.Items.Add(new Metadata.ItemsData
                {
                    Key = e.Key.ToString(),
                    Value = e.Value.ToString()
                });
            }
            return metadata;
        }
    }
}
