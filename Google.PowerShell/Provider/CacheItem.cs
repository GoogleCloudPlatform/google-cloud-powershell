// Copyright 2016 Google Inc. All Rights Reserved.
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
        private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;
        private readonly TimeSpan _cacheLifetime;
        private T _value;
        private readonly Func<T> _update;

        public T Value
        {
            get
            {
                return GetValueWithUpdateFunction(_update);
            }
        }

        public bool CacheOutOfDate
        {
            get
            {
                return DateTimeOffset.Now > _lastUpdate + _cacheLifetime;
            }
        }

        public T GetValueWithUpdateFunction(Func<T> updateFunc)
        {
            if (CacheOutOfDate && updateFunc != null)
            {
                _value = updateFunc();
                _lastUpdate = DateTimeOffset.Now;
            }
            return _value;
        }

        public T GetLastValueWithoutUpdate()
        {
            return _value;
        }

        public CacheItem(Func<T> update) : this(update, TimeSpan.FromMinutes(1)) { }

        public CacheItem() : this(TimeSpan.FromMinutes(1)) { }

        public CacheItem(TimeSpan cacheLifetime)
        {
            _cacheLifetime = cacheLifetime;
        }

        public CacheItem(Func<T> update, TimeSpan cacheLifetime)
        {
            _update = update;
            _cacheLifetime = cacheLifetime;
        }
    }
}
