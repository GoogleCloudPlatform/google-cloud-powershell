using NUnit.Framework;
using Google.Apis.Util;
using Google.PowerShell.Common;
using System;
using System.Reflection;
using System.Threading;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    internal class TestAuthenticateWithSdkCredentialsExecutor
    {
        private PropertyInfo tokenProperty = typeof(AuthenticateWithSdkCredentialsExecutor).GetProperty(
                "Token",
                BindingFlags.NonPublic | BindingFlags.Static);
        private CancellationToken cancelToken = new CancellationToken();

        /// <summary>
        /// This test checks that an ActiveUserToken is created when a new
        /// AuthenticateWithSdkCredentialsExecutor is created.
        /// </summary>
        [Test]
        public void TestAuthenticateWithSdkCredentialsExecutorConstructor()
        {
            AuthenticateWithSdkCredentialsExecutor activeUserCred = new AuthenticateWithSdkCredentialsExecutor();
            ActiveUserToken activeUserToken = tokenProperty.GetValue(null, null) as ActiveUserToken;
            Assert.IsNotNull(activeUserToken, "ActiveUserToken should be created.");
        }

        /// <summary>
        /// This test checks that a call to RefreshTokenAsync will give us a new token.
        /// </summary>
        [Test]
        public void TestRefreshTokenAsync()
        {
            AuthenticateWithSdkCredentialsExecutor activeUserCred = new AuthenticateWithSdkCredentialsExecutor();
            ActiveUserToken activeUserToken = tokenProperty.GetValue(null, null) as ActiveUserToken;

            bool refreshed = activeUserCred.RefreshTokenAsync(cancelToken).Result;

            Assert.IsTrue(refreshed, "RefreshTokenAsync should return true.");

            ActiveUserToken refreshedActiveUserToken = tokenProperty.GetValue(null, null) as ActiveUserToken;

            Assert.IsTrue(
                !Equals(activeUserToken.AccessToken, refreshedActiveUserToken.AccessToken),
                "Refreshed ActiveUserToken should have a different access token.");
        }

        /// <summary>
        /// This test checks that GetAccessTokenForRequestAsync works.
        /// </summary>
        [Test]
        public void TestGetAccessTokenForRequestAsync()
        {
            AuthenticateWithSdkCredentialsExecutor activeUserCred = new AuthenticateWithSdkCredentialsExecutor();
            ActiveUserToken activeUserToken = tokenProperty.GetValue(null, null) as ActiveUserToken;

            string accessToken = activeUserCred.GetAccessTokenForRequestAsync(null, cancelToken).Result;

            // The access token returned by GetAccessTokenForRequestAsync should come from token.
            Assert.IsTrue(
                Equals(activeUserToken.AccessToken, accessToken),
                "GetAccessTokenForRefreshAsync returns the wrong access token.");

            // The next call to GetAccessTokenForRequestAsync should returns the same token.
            accessToken = activeUserCred.GetAccessTokenForRequestAsync(null, cancelToken).Result;
            Assert.IsTrue(
                Equals(activeUserToken.AccessToken, accessToken),
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
            ActiveUserToken activeUserToken = tokenProperty.GetValue(null, null) as ActiveUserToken;

            // Force the access token to expire.
            activeUserToken.Issued = DateTime.Now.AddSeconds(-activeUserToken.ExpiresInSeconds.Value);
            Assert.IsTrue(activeUserToken.IsExpired(SystemClock.Default), "ActiveUserToken should be expired");

            string newAccessToken = activeUserCred.GetAccessTokenForRequestAsync(null, cancelToken).Result;
            Assert.IsFalse(
                Equals(activeUserToken.AccessToken, newAccessToken),
                "GetAccessTokenForRefreshAsync should returns a new access token when the old one expired due to time.");

            // The next call to GetAccessTokenForRequestAsync should returns the same token.
            string anotherNewAccessToken = activeUserCred.GetAccessTokenForRequestAsync(null, cancelToken).Result;
            Assert.IsTrue(
                Equals(anotherNewAccessToken, newAccessToken),
                "GetAccessTokenForRefreshAsync should returns a new access token when the old one expired due to time.");
        }

        /// <summary>
        /// This test checks that GetAccessTokenForRequestAsync returns a new token
        /// when the active user is changed.
        /// </summary>
        [Test]
        public void TestGetAcessTokenExpiredByChangingUser()
        {
            AuthenticateWithSdkCredentialsExecutor activeUserCred = new AuthenticateWithSdkCredentialsExecutor();
            ActiveUserToken activeUserToken = tokenProperty.GetValue(null, null) as ActiveUserToken;

            // Invalidate the token by changing the active user.
            FieldInfo activeUserField = typeof(ActiveUserToken).GetField(
                "activeUser",
                BindingFlags.NonPublic | BindingFlags.Instance);
            activeUserField.SetValue(activeUserToken, "A new user");

            Assert.IsTrue(activeUserToken.IsExpired(SystemClock.Default), "ActiveUserToken should be expired");

            string newAccessToken = activeUserCred.GetAccessTokenForRequestAsync(null, cancelToken).Result;
            Assert.IsFalse(
                Equals(activeUserToken.AccessToken, newAccessToken),
                "GetAccessTokenForRefreshAsync should returns a new access token when the old one is invalidated.");

            // The next call to GetAccessTokenForRequestAsync should returns the same token.
            string anotherNewAccessToken = activeUserCred.GetAccessTokenForRequestAsync(null, cancelToken).Result;
            Assert.IsTrue(
                Equals(anotherNewAccessToken, newAccessToken),
                "GetAccessTokenForRefreshAsync should returns a new access token when the old one is invalidated.");
        }
    }
}

