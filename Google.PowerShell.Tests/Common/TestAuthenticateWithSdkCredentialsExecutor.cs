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
        private FieldInfo tokenProperty = typeof(AuthenticateWithSdkCredentialsExecutor).GetField(
                "s_token",
                BindingFlags.NonPublic | BindingFlags.Static);
        private CancellationToken cancelToken = new CancellationToken();

        /// <summary>
        /// Sets the s_token field to null before each test.
        /// </summary>
        [SetUp]
        public void init()
        {
            tokenProperty.SetValue(null, null);
        }

        /// <summary>
        /// This test checks that a call to RefreshTokenAsync will give us a new token.
        /// </summary>
        [Test]
        public void TestRefreshTokenAsync()
        {
            AuthenticateWithSdkCredentialsExecutor activeUserCred = new AuthenticateWithSdkCredentialsExecutor();
            object activeUserToken = tokenProperty.GetValue(null);

            Assert.IsNull(activeUserToken, "s_token should be null initially.");

            bool refreshed = activeUserCred.RefreshTokenAsync(cancelToken).Result;
            Assert.IsTrue(refreshed, "RefreshTokenAsync should return true.");

            ActiveUserToken refreshedToken = tokenProperty.GetValue(null) as ActiveUserToken;
            Assert.IsNotNull(refreshedToken, "RefreshTokenAsync should set s_token to a non-null token.");
            Assert.IsNotNullOrEmpty(refreshedToken.AccessToken, "s_token should have a valid access token.");

            // We refresh again to make sure we get a different token.
            refreshed = activeUserCred.RefreshTokenAsync(cancelToken).Result;
            Assert.IsTrue(refreshed, "RefreshTokenAsync should return true.");

            ActiveUserToken secondRefreshedToken = tokenProperty.GetValue(null) as ActiveUserToken;
            Assert.IsNotNull(secondRefreshedToken, "RefreshTokenAsync should set s_token to a non-null token.");
            Assert.IsNotNullOrEmpty(secondRefreshedToken.AccessToken, "s_token should have a valid access token.");
            Assert.IsTrue(
                !Equals(refreshedToken.AccessToken, secondRefreshedToken.AccessToken),
                "A different token should be returned when RefreshTokenAsync is called again.");
        }

        /// <summary>
        /// This test checks that GetAccessTokenForRequestAsync works.
        /// </summary>
        [Test]
        public void TestGetAccessTokenForRequestAsync()
        {
            AuthenticateWithSdkCredentialsExecutor activeUserCred = new AuthenticateWithSdkCredentialsExecutor();
            // We have to call GetAccessTokenForRequestAsync first for the s_token to be generated.
            string accessToken = activeUserCred.GetAccessTokenForRequestAsync(null, cancelToken).Result;
            ActiveUserToken activeUserToken = tokenProperty.GetValue(null) as ActiveUserToken;

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
            // Generate the s_token.
            activeUserCred.RefreshTokenAsync(new CancellationToken()).Wait();
            ActiveUserToken activeUserToken = tokenProperty.GetValue(null) as ActiveUserToken;

            // Force the access token to expire.
            activeUserToken.ExpiredTime = DateTime.UtcNow.AddSeconds(-100);
            Assert.IsTrue(activeUserToken.IsExpiredOrInvalid(), "ActiveUserToken should be expired");

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
            // Generate the s_token.
            activeUserCred.RefreshTokenAsync(new CancellationToken()).Wait();
            ActiveUserToken activeUserToken = tokenProperty.GetValue(null) as ActiveUserToken;

            // Invalidate the token by changing the active user.
            FieldInfo activeUserField = typeof(ActiveUserToken).GetField(
                "activeUser",
                BindingFlags.NonPublic | BindingFlags.Instance);
            activeUserField.SetValue(activeUserToken, "A new user");

            Assert.IsTrue(activeUserToken.IsExpiredOrInvalid(), "ActiveUserToken should be expired");

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

