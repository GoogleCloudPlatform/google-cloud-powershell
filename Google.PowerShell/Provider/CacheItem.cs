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

using System;

namespace Google.PowerShell.Provider
{
    /// <summary>
    /// A CacheItem will get an object from the given update function and continue to return that object for
    /// the duration of cacheLifetime. It will then make another call to the update function to get a new 
    /// version of the object. The default cache lifetime is one minute.
    /// </summary>
    public class CacheItem<T>
    {
        private static readonly TimeSpan s_minuteTimeSpan = TimeSpan.FromMinutes(1);
        private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;
        private readonly TimeSpan _cacheLifetime;
        private T _value;
        private readonly Func<T> _update;
        internal Func<DateTimeOffset> DateTimeOffsetNow = () => DateTimeOffset.Now;

        /// <summary>
        /// Get the value after applying the default update function
        /// if the value is out of date.
        /// </summary>
        public T Value => GetValueWithUpdateFunction(_update);

        /// <summary>
        /// Initialize a CacheItem with a cache reset time set to cacheLifetime
        /// and update function set to update.
        /// </summary>
        /// <param name="update">
        /// Update function that is used to update the value if cache is out of date.
        /// Default to null, which means GetValueWithUpdateFunction, GetLastValueWithoutUpdate
        /// and Value will return default value for type T.
        /// </param>
        /// <param name="cacheLifetime">Time span that the cache is valid for. Default to 1 minute.</param>
        public CacheItem(Func<T> update = null, TimeSpan? cacheLifetime = null)
        {
            _update = update;
            _cacheLifetime = cacheLifetime ?? s_minuteTimeSpan;
        }

        /// <summary>
        /// Returns true if the cache is out of date.
        /// </summary>
        public bool CacheOutOfDate()
        {
            return DateTimeOffsetNow() > _lastUpdate + _cacheLifetime;
        }

        /// <summary>
        /// Get the value after applying an update function to the value
        /// if the value is out of date.
        /// </summary>
        /// <param name="updateFunc">The update function that is used to update _value.</param>
        /// <returns>Returns the updated value.</returns>
        public T GetValueWithUpdateFunction(Func<T> updateFunc)
        {
            if (CacheOutOfDate() && updateFunc != null)
            {
                _value = updateFunc();
                _lastUpdate = DateTimeOffsetNow();
            }
            return _value;
        }

        /// <summary>
        /// Get the last stored value without calling the update function.
        /// </summary>
        public T GetLastValueWithoutUpdate()
        {
            return _value;
        }

        /// <summary>
        /// Resets the Cache to update at the next opportunity.
        /// </summary>
        public void Reset()
        {
            _lastUpdate = DateTimeOffset.MinValue;
        }
    }
}
