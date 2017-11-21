// Copyright 2015-2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.PowerShell.Provider;
using NUnit.Framework;
using System;

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
            cacheItem.DateTimeOffsetNow = () => DateTimeOffset.Now.AddSeconds(61);
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
            var cacheItem = new CacheItem<string>(() => changingString, cacheLifetime: MinuteTimeSpan);
            Assert.AreEqual(cacheItem.Value, before, "Cache has the wrong value.");
            changingString = after;
            // Wait for a minute before calling Value again so the item will be updated.
            cacheItem.DateTimeOffsetNow = () => DateTimeOffset.Now.AddSeconds(61);
            Assert.IsTrue(cacheItem.CacheOutOfDate(), "Cache should be out of date after the timespan.");
            Assert.AreEqual(cacheItem.GetLastValueWithoutUpdate(), before, "GetLastValueWithoutUpdate should return value before the update.");
            Assert.AreEqual(cacheItem.Value, after, "Cache has the wrong value.");
        }
    }
}
