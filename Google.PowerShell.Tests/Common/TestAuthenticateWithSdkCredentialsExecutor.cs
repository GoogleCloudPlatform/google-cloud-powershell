// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using NUnit.Framework;
using Google.PowerShell.Common;
using System;
using System.Reflection;
using System.Threading;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class TestAuthenticateWithSdkCredentialsExecutor
    {
        private CancellationToken _cancelToken = new CancellationToken();

        /// <summary>
        /// This test checks that a call to RefreshTokenAsync will give us a new token.
        /// </summary>
        [Test]
        public void TestRefreshTokenAsync()
        {
            AuthenticateWithSdkCredentialsExecutor activeUserCred = new AuthenticateWithSdkCredentialsExecutor();
            string currentAccessToken = activeUserCred.GetAccessTokenForRequestAsync().Result;

            bool refreshed = activeUserCred.RefreshTokenAsync(_cancelToken).Result;
            Assert.IsTrue(refreshed, "RefreshTokenAsync should return true.");

            string refreshedAccessToken = activeUserCred.GetAccessTokenForRequestAsync().Result;
            Assert.IsTrue(
                !Equals(currentAccessToken, refreshedAccessToken),
                "A different token should be returned when RefreshTokenAsync is called again.");

            refreshed = activeUserCred.RefreshTokenAsync(_cancelToken).Result;
            Assert.IsTrue(refreshed, "RefreshTokenAsync should return true.");

            string refreshedAccessTokenTwo = activeUserCred.GetAccessTokenForRequestAsync().Result;
            Assert.IsTrue(
                !Equals(refreshedAccessToken, refreshedAccessTokenTwo),
                "A different token should be returned when RefreshTokenAsync is called again.");
            Assert.IsTrue(
                !Equals(currentAccessToken, refreshedAccessTokenTwo),
                "A different token should be returned when RefreshTokenAsync is called again.");
        }

        /// <summary>
        /// This test checks that GetAccessTokenForRequestAsync works.
        /// </summary>
        [Test]
        public void TestGetAccessTokenForRequestAsync()
        {
            AuthenticateWithSdkCredentialsExecutor activeUserCred = new AuthenticateWithSdkCredentialsExecutor();
            string accessToken = activeUserCred.GetAccessTokenForRequestAsync().Result;

            TokenResponse activeToken = ActiveUserConfig.GetActiveUserToken(_cancelToken).Result;
            // The access token returned by GetAccessTokenForRequestAsync should be the same as that of active user config.
            Assert.IsTrue(
                Equals(activeToken.AccessToken, accessToken),
                "GetAccessTokenForRefreshAsync returns the wrong access token.");

            // The next call to GetAccessTokenForRequestAsync should returns the same token.
            accessToken = activeUserCred.GetAccessTokenForRequestAsync().Result;
            Assert.IsTrue(
                Equals(activeToken.AccessToken, accessToken),
                "GetAccessTokenForRefreshAsync returns the wrong access token.");
        }

        /// <summary>
        /// This test checks that GetAccessTokenForRequestAsync returns a new token
        /// when the old one expired due to time.
        /// </summary>
        [Test]
        public void TestGetAccessTokenExpiredByTime()
        {
            AuthenticateWithSdkCredentialsExecutor activeUserCred = new AuthenticateWithSdkCredentialsExecutor();
            TokenResponse activeToken = ActiveUserConfig.GetActiveUserToken(_cancelToken).Result;

            // Force the access token to expire.
            activeToken.ExpiredTime = DateTime.UtcNow.AddSeconds(-100);
            Assert.IsTrue(activeToken.IsExpired, "TokenResponse should be expired");

            string newAccessToken = activeUserCred.GetAccessTokenForRequestAsync().Result;
            Assert.IsFalse(
                Equals(activeToken.AccessToken, newAccessToken),
                "GetAccessTokenForRefreshAsync should returns a new access token when the old one expired due to time.");

            // The next call to GetAccessTokenForRequestAsync should returns the same token.
            string anotherNewAccessToken = activeUserCred.GetAccessTokenForRequestAsync().Result;
            Assert.IsTrue(
                Equals(anotherNewAccessToken, newAccessToken),
                "GetAccessTokenForRefreshAsync should returns a new access token when the old one expired due to time.");
        }
    }
}
