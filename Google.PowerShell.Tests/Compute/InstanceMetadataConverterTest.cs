// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1.Data;
using Google.PowerShell.ComputeEngine;
using NUnit.Framework;
using System.Collections;
using System.Linq;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class InstanceMetadataConverterTest
    {
        [Test]
        public void TestConversion()
        {
            Metadata metadata = InstanceMetadataPSConverter.BuildMetadata(new Hashtable
            {
                {"key", "value"},
                {"key2", "value2"}
            });
            Assert.IsNotNull(metadata);

            Assert.AreEqual(2, metadata.Items.Count);
            Assert.That(metadata.Items.Count(i => i.Key == "key") == 1);
            Assert.That(metadata.Items.Count(i => i.Key == "key2") == 1);
            Assert.AreEqual("value", metadata.Items.Where(i => i.Key == "key").Select(i => i.Value).First());
            Assert.AreEqual("value2", metadata.Items.Where(i => i.Key == "key2").Select(i => i.Value).First());
        }
    }
}
