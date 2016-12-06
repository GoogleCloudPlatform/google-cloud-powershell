// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Provider;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using System;
using System.Threading;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class CacheItemTest
    {
        public static string TestString = "string";
        public static TimeSpan MinuteTimeSpan = TimeSpan.FromMinutes(1);

        [Test]
        public void TestCacheOutOfDate()
        {
            var cacheItem = new CacheItem<string>(() => TestString, MinuteTimeSpan);
            // Call Value to update cache.
            Assert.AreEqual(TestString, cacheItem.Value, "Cache has the wrong value.");
            Assert.IsFalse(cacheItem.CacheOutOfDate(), "Cache should not be out of date yet.");
            Thread.Sleep(MinuteTimeSpan);
            Assert.IsTrue(cacheItem.CacheOutOfDate(), "Cache should be out of date after the timespan.");

            // Now we call Value to update cache again.
            Assert.AreEqual(TestString, cacheItem.Value, "Cache has the wrong value.");
            Assert.IsFalse(cacheItem.CacheOutOfDate(), "Cache should not be out of date yet.");
        }

        [Test]
        public void TestGetValueWithUpdateFunction()
        {
            var cacheItem = new CacheItem<string>(cacheLifetime: MinuteTimeSpan);
            // Without update function, we will get a null or empty string.
            Assert.IsTrue(string.IsNullOrEmpty(cacheItem.Value), "Cache should return default value without update function.");
            string value = cacheItem.GetValueWithUpdateFunction(() => TestString);
            Assert.AreEqual(value, TestString, "GetValueWithUpdateFunction returns the incorrect value.");
            Assert.IsFalse(cacheItem.CacheOutOfDate(), "Cache should not be out of date yet.");
        }

        [Test]
        public void TestGetValueWithoutUpdateFunction()
        {
            string before = "before";
            string after = "after";
            string changingString = before;
            // This function returns changingString so if we change changingString, the function will return a different value.
            Func<string> testFunction = () =>
            {
                return changingString;
            };

            var cacheItem = new CacheItem<string>(testFunction, cacheLifetime: MinuteTimeSpan);
            Assert.AreEqual(cacheItem.Value, before, "Cache has the wrong value.");
            changingString = after;
            // Wait for a minute before calling Value again so the item will be updated.
            Thread.Sleep(MinuteTimeSpan);
            Assert.IsTrue(cacheItem.CacheOutOfDate(), "Cache should be out of date after the timespan.");
            Assert.AreEqual(cacheItem.GetLastValueWithoutUpdate(), before, "GetLastValueWithoutUpdate should return value before the update.");
            Assert.AreEqual(cacheItem.Value, after, "Cache has the wrong value.");
        }
    }
}
