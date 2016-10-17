using NUnit.Framework;
using Google.Apis.Util;
using Google.PowerShell.Common;
using System;
using System.Reflection;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    /// <summary>
    /// Assumes that the Cloud SDK is installed on the local machine and initialized.
    /// 
    /// </summary>
    internal class TestActiveUserToken
    {
        /// <summary>
        /// This test checks that if the active user is changed, the token should be considered as expired.
        /// </summary>
        [Test]
        public void TestTokenExpiredByActiveUser()
        {
            ActiveUserToken token = new ActiveUserToken();
            // If we don't set the access token, the token will be treated as expired.
            token.AccessToken = "Random Access Token";
            token.ExpiresInSeconds = 3600;
            token.Issued = DateTime.Now;

            Assert.IsFalse(token.IsExpired(SystemClock.Default));

            FieldInfo activeUserField = typeof(ActiveUserToken).GetField(
                "activeUser",
                BindingFlags.NonPublic | BindingFlags.Instance);

            activeUserField.SetValue(token, "A new user");

            Assert.IsTrue(
                token.IsExpired(SystemClock.Default),
                "Token should have expired when the active user was changed,");
        }

        /// <summary>
        /// This test checks that if enough time has passed, the token should be considered as expired.
        /// </summary>
        [Test]
        public void TestTokenExpiredByTime()
        {
            ActiveUserToken token = new ActiveUserToken();
            // If we don't set the access token, the token will be treated as expired.
            token.AccessToken = "Random Access Token";
            token.ExpiresInSeconds = 3600;

            token.Issued = DateTime.Now.AddSeconds(-1800);
            Assert.IsFalse(token.IsExpired(SystemClock.Default), "Token should not expire yet.");

            token.Issued = DateTime.Now.AddSeconds(-3591);
            Assert.IsTrue(token.IsExpired(SystemClock.Default), "Token should have expired 1 minute before.");
        }
    }
}

