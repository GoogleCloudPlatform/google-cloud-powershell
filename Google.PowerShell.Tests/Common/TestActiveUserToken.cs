// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using NUnit.Framework;
using Google.PowerShell.Common;
using System;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    /// <summary>
    /// Assumes that the Cloud SDK is installed on the local machine and initialized.
    /// 
    /// </summary>
    public class TestActiveUserToken
    {
        private string _credentialJson = @"{
            'access_token': 'access token'
        }".Replace('\'', '\"');

        private string _credentialJsonWithNullExpiry = @"{
            'access_token': 'access token',
            'token_expiry': null
        }".Replace('\'', '\"');

        private string _credentialJsonWithExpiry = @"{
            'access_token': 'access token',
            'token_expiry': {
                'year': 2016,
                'month': 10,
                'day': 18,
                'hour': 20,
                'minute': 36,
                'second': 4,
                'microsecond': 497000
            }
        }".Replace('\'', '\"');

        [Test]
        public void TestCredentialJsonWithTokenExpiry()
        {
            ActiveUserToken token = new ActiveUserToken(_credentialJsonWithExpiry, "user");
            Assert.IsTrue(Equals(token.AccessToken, "access token"), "AccessToken does not match the value in JSON string.");
            Assert.IsTrue(
                Equals(token.ExpiredTime, new DateTime(2016, 10, 18, 20, 36, 4, 497, DateTimeKind.Utc)),
                "ExpiredTime does not match the value in JSON string.");
        }

        [Test]
        public void TestCredentialJsonWithNullTokenExpiry()
        {
            ActiveUserToken token = new ActiveUserToken(_credentialJsonWithNullExpiry, "user");
            Assert.IsTrue(
                Equals(token.ExpiredTime, DateTime.MaxValue),
                "ExpiredTime should be set to DateTime.MaxValue if token_expiry is null");
        }

        /// <summary>
        /// This test checks that if enough time has passed, the token should be considered as expired.
        /// </summary>
        [Test]
        public void TestTokenExpiredByTime()
        {
            ActiveUserToken token = new ActiveUserToken(_credentialJson, "user");
            token.ExpiredTime = DateTime.UtcNow.AddHours(1);

            Assert.IsFalse(token.IsExpired, "Token should not expire yet.");

            token.ExpiredTime = DateTime.UtcNow.AddMinutes(-1);
            Assert.IsTrue(token.IsExpired, "Token should have expired 1 minute before.");
        }
    }
}

