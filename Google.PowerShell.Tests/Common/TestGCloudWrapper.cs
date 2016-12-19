// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using NUnit.Framework;
using Google.PowerShell.Common;
using Newtonsoft.Json.Linq;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class TestGCloudWrapper
    {
        /// <summary>
        /// Test that GetActiveConfig returns a valid config.
        /// </summary>
        [Test]
        public void TestGetActiveConfig()
        {
            string config = GCloudWrapper.GetActiveConfig().Result;
            Assert.IsNotNull(config);

            JToken parsedConfigJson = JObject.Parse(config);
            Assert.IsNotNull(parsedConfigJson.SelectToken("sentinels.config_sentinel"),
                "Config returned by GetActiveConfig should have a sentinel file.");
            Assert.IsNotNull(parsedConfigJson.SelectToken("credential"),
                "Config returned by GetActiveConfig should have a credential property.");
            Assert.IsNotNull(parsedConfigJson.SelectToken("configuration.properties"),
                "Config returned by GetActiveConfig should have a configuration.");
        }
    }
}
