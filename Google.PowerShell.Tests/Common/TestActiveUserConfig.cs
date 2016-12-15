// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using NUnit.Framework;
using Google.PowerShell.Common;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class TestActiveUserConfig
    {
        private string _userConfigJson = @"{
            'configuration': {
                'active_configuration': 'testing',
                'properties': {
                    'compute': {
                        'region': 'us-central1',
                        'zone': 'us-central1-f'
                    },
                    'core': {
                        'account': 'testing@google.com',
                        'disable_usage_reporting': 'False',
                        'project': 'gcloud-powershell-testing'
                    }
                }
            },
            'credential': {
                'access_token': 'access_token',
                'token_expiry': '2012-12-12T12:12:12Z'
            },
            'sentinels': {
                'config_sentinel': 'sentinel.sentinel'
            }
        }".Replace('\'', '\"');

        /// <summary>
        /// Tests that active user config returns correct access token and sentinel file.
        /// </summary>
        [Test]
        public void TestActiveUserConfigConstructor()
        {
            ActiveUserConfig userConfig = new ActiveUserConfig(_userConfigJson);
            string sentinelFile = "sentinel.sentinel";
            string accessToken = "access_token";
            DateTime expiredTime = DateTime.Parse("2012-12-12T12:12:12Z").ToUniversalTime();

            Assert.AreEqual(sentinelFile, userConfig.SentinelFile, "Failed to get the sentinel file from config JSON.");
            Assert.AreEqual(accessToken, userConfig.UserToken.AccessToken, "Token response created has the wrong access token.");
            Assert.AreEqual(expiredTime, userConfig.UserToken.ExpiredTime, "Token response created has the wrong expiry time");
        }

        /// <summary>
        /// Tests that TryGetPropertyValue works.
        /// </summary>
        [Test]
        public void TestTryGetPropertyValue()
        {
            ActiveUserConfig userConfig = new ActiveUserConfig(_userConfigJson);
            string result = null;

            string region = "us-central1";
            ActiveUserConfig.TryGetPropertyValue(userConfig.PropertiesJson, "region", ref result);
            Assert.AreEqual(region, result, "Failed to get property region from config JSON.");

            string zone = "us-central1-f";
            ActiveUserConfig.TryGetPropertyValue(userConfig.PropertiesJson, "zone", ref result);
            Assert.AreEqual(zone, result, "Failed to get property zone from config JSON.");

            string project = "gcloud-powershell-testing";
            ActiveUserConfig.TryGetPropertyValue(userConfig.PropertiesJson, "project", ref result);
            Assert.AreEqual(project, result, "Failed to get property project from config JSON.");
        }

        /// <summary>
        /// Tests that GetActiveUserConfig works.
        /// </summary>
        [Test]
        public void TestGetActiveUserConfig()
        {
            ActiveUserConfig userConfig = ActiveUserConfig.GetActiveUserConfig().Result;

            Assert.IsNotNull(userConfig, "GetActiveUserConfig should not return null.");
            Assert.IsNotNull(userConfig.CachedFingerPrint, "Config returned should have a cached fingerprint.");
            Assert.IsNotNull(userConfig.SentinelFile, "Config returned should have a sentinel file.");
            Assert.IsNotNull(userConfig.UserToken, "Config returned should have a user token.");
            Assert.IsFalse(userConfig.UserToken.IsExpired, "Config returned should have an unexpired user token.");
            Assert.IsNotNullOrEmpty(userConfig.UserToken.AccessToken, "Config returned should have an access token.");

            // Test that the result is cached.
            ActiveUserConfig userConfig2 = ActiveUserConfig.GetActiveUserConfig().Result;
            Assert.AreEqual(userConfig, userConfig2, "GetActiveUserConfig should cache the result.");

            // Test that refresh will refresh the cache.
            userConfig2 = ActiveUserConfig.GetActiveUserConfig(refreshConfig: true).Result;
            Assert.AreNotEqual(userConfig, userConfig2, "GetActiveUserConfig should not cache the result if refreshConfig is true.");
        }

        /// <summary>
        /// Tests that GetActiveUserToken works.
        /// </summary>
        [Test]
        public void TestGetActiveUserToken()
        {
            CancellationToken cancellationToken = new CancellationToken();
            TokenResponse activeToken = ActiveUserConfig.GetActiveUserToken(cancellationToken).Result;

            Assert.IsNotNull(activeToken, "GetActiveUserToken should return a user token.");
            Assert.IsFalse(activeToken.IsExpired, "GetActiveUserToken should not return an expired token.");
            Assert.IsNotNullOrEmpty(activeToken.AccessToken, "GetActiveUserToken should return a token with access token.");

            // Test that the result is cached.
            TokenResponse activeToken2 = ActiveUserConfig.GetActiveUserToken(cancellationToken).Result;
            Assert.AreEqual(activeToken, activeToken2, "GetActiveUserToken should cache the result.");

            // Test that refresh will refresh the cache.
            activeToken2 = ActiveUserConfig.GetActiveUserToken(cancellationToken, refresh: true).Result;
            Assert.AreNotEqual(
                activeToken.AccessToken,
                activeToken2.AccessToken,
                "GetActiveUserToken returns a new token with a new access token.");
        }
    }
}
