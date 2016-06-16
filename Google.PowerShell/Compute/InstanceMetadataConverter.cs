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
    public class InstanceMetadataPSConverter : PSTypeConverter
    {
        public override bool CanConvertFrom(object sourceValue, Type destinationType)
        {
            return sourceValue is Metadata && destinationType.IsAssignableFrom(typeof(Hashtable));
        }

        public override object ConvertFrom(object sourceValue, Type destinationType, IFormatProvider formatProvider, bool ignoreCase)
        {
            var table = new Hashtable();
            var metadata = sourceValue as Metadata;
            if (metadata == null)
            {
                return null;
            }

            foreach (var item in metadata.Items)
            {
                table.Add(item.Key, item.Value);
            }
            return table;
        }

        public override bool CanConvertTo(object sourceValue, Type destinationType)
        {
            return sourceValue is IDictionary && destinationType.IsAssignableFrom(typeof(Metadata));
        }

        public override object ConvertTo(object sourceValue, Type destinationType, IFormatProvider formatProvider, bool ignoreCase)
        {

            var table = sourceValue as IDictionary;
            if (table == null)
            {
                return null;
            }

            var metadata = BuildMetadata(table);
            return metadata;
        }

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