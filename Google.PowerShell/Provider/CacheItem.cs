// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

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
        private static TimeSpan s_minuteTimeSpan = TimeSpan.FromMinutes(1);
        private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;
        private readonly TimeSpan _cacheLifetime;
        private T _value;
        private readonly Func<T> _update;

        /// <summary>
        /// Get the value after applying the default update function
        /// if the value is out of date.
        /// </summary>
        public T Value
        {
            get
            {
                return GetValueWithUpdateFunction(_update);
            }
        }

        /// <summary>
        /// Returns true if the cache is out of date.
        /// </summary>
        public bool CacheOutOfDate()
        {
            return DateTimeOffset.Now > _lastUpdate + _cacheLifetime;
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
                _lastUpdate = DateTimeOffset.Now;
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
    }
}
