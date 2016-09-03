// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;

namespace Google.PowerShell.Provider
{
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
                if (DateTimeOffset.Now > _lastUpdate + _cacheLifetime)
                {
                    _value = _update();
                    _lastUpdate = DateTimeOffset.Now;
                }
                return _value;
            }
        }

        public CacheItem(Func<T> update) : this(update, TimeSpan.FromMinutes(1)) { }

        public CacheItem(Func<T> update, TimeSpan cacheLifetime)
        {
            _update = update;
            _cacheLifetime = cacheLifetime;
        }
    }
}
