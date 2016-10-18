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
        private string credentialJson = @"{
            ""access_token"": ""access token""
        }";

        private string credentialJsonWithNullExpiry = @"{
            ""access_token"": ""access token"",
            ""token_expiry"": null
        }";

        private string credentialJsonWithExpiry = @"{
            ""access_token"": ""access token"",
            ""token_expiry"": {
                ""year"": 2016,
                ""month"": 10,
                ""day"": 18,
                ""hour"": 20,
                ""minute"": 36,
                ""second"": 4,
                ""microsecond"": 497000
            }
        }";

        [Test]
        public void TestCredentialJsonWithTokenExpiry()
        {
            ActiveUserToken token = new ActiveUserToken(credentialJsonWithExpiry, () => { return "user"; });
            Assert.IsTrue(Equals(token.AccessToken, "access token"), "AccessToken does not match the value in JSON string.");
            Assert.IsTrue(
                Equals(token.ExpiredTime, new DateTime(2016, 10, 18, 20, 36, 4, 497, DateTimeKind.Utc)),
                "ExpiredTime does not match the value in JSON string.");
        }

        [Test]
        public void TestCredentialJsonWithNullTokenExpiry()
        {
            ActiveUserToken token = new ActiveUserToken(credentialJsonWithNullExpiry, () => { return "user"; });
            Assert.IsTrue(
                Equals(token.ExpiredTime, DateTime.MaxValue),
                "ExpiredTime should be set to DateTime.MaxValue if token_expiry is null");
        }

        /// <summary>
        /// This test checks that if the active user is changed, the token should be considered as expired.
        /// </summary>
        [Test]
        public void TestTokenExpiredByActiveUser()
        {
            ActiveUserToken token = new ActiveUserToken(credentialJson, () => { return "user"; });
            token.ExpiredTime = DateTime.UtcNow.AddHours(1);

            Assert.IsFalse(token.IsExpiredOrInvalid());

            token.getActiveUser = () => { return "differentUser"; };
            Assert.IsTrue(
                token.IsExpiredOrInvalid(),
                "Token should have expired when the active user was changed,");
        }

        /// <summary>
        /// This test checks that if enough time has passed, the token should be considered as expired.
        /// </summary>
        [Test]
        public void TestTokenExpiredByTime()
        {
            ActiveUserToken token = new ActiveUserToken(credentialJson, () => { return "user"; });
            // If we don't set the access token, the token will be treated as expired.
            token.AccessToken = "Random Access Token";
            token.ExpiredTime = DateTime.UtcNow.AddHours(1);

            Assert.IsFalse(token.IsExpiredOrInvalid(), "Token should not expire yet.");

            token.ExpiredTime = DateTime.UtcNow.AddMinutes(-1);
            Assert.IsTrue(token.IsExpiredOrInvalid(), "Token should have expired 1 minute before.");
        }
    }
}

