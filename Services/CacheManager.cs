using System.Collections.Concurrent;
using log4net;

namespace Manager;

public class CacheManager : ICacheManager
{
    private readonly ILog _logger;
    private readonly ConcurrentDictionary<string, object> _cache;
    private readonly int _maxItems;
    private readonly object _capacityLock = new();

    public CacheManager(int maxItems)
    {
        _logger = LogManager.GetLogger(typeof(CacheManager));

        if (maxItems <= 0)
        {
            _logger.Error($"{CacheServerConstants.InvalidCacheSize}: {maxItems}");
            throw new ArgumentOutOfRangeException(
                nameof(maxItems),
                CacheServerConstants.CacheSizeMustBeGreaterThanZero);
        }

        _maxItems = maxItems;
        _cache = new ConcurrentDictionary<string, object>();

        _logger.Info(
            string.Format(
                CacheServerConstants.CacheInitialized,
                _maxItems));
    }

    public bool Create(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(CacheServerConstants.CreateEmptyKey);
            return false;
        }

        lock (_capacityLock)
        {
            if (_cache.Count >= _maxItems)
            {
                _logger.Warn(
                     string.Format(
                     CacheServerConstants.CacheCapacityReached,
                    _maxItems,
                    _cache.Count));

                throw new InvalidOperationException(
                    CacheServerConstants.CacheIsFull);
            }

            bool added = _cache.TryAdd(key, value);

            if (added)
            {
                _logger.Info(
                    string.Format(
                        CacheServerConstants.CreateSuccess,
                        key));
            }
            else
            {
                _logger.Warn(
                    string.Format(
                        CacheServerConstants.CreateDuplicate,
                        key));
            }

            return added;
        }
    }

    public object Read(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(CacheServerConstants.ReadEmptyKey);
            return null;
        }

        if (_cache.TryGetValue(key, out var value))
        {
            _logger.Debug(
                string.Format(
                    CacheServerConstants.CacheHit,
                    key));
            return value;
        }

        _logger.Debug(
            string.Format(
                CacheServerConstants.CacheMiss,
                key));

        return null;
    }

    public bool Update(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(CacheServerConstants.UpdateEmptyKey);
            return false;
        }

        if (!_cache.ContainsKey(key))
        {
            _logger.Warn(
                string.Format(
                    CacheServerConstants.UpdateMissingKey,
                    key));
            return false;
        }

        _cache[key] = value;

        _logger.Info(
            string.Format(
                CacheServerConstants.UpdateSuccess,
                key));

        return true;
    }

    public bool Delete(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(CacheServerConstants.DeleteEmptyKey);
            return false;
        }

        bool removed = _cache.TryRemove(key, out _);

        if (removed)
        {
            _logger.Info(
                string.Format(
                    CacheServerConstants.DeleteSuccess,
                    key));
        }
        else
        {
            _logger.Warn(
                string.Format(
                    CacheServerConstants.DeleteMissingKey,
                    key));
        }

        return removed;
    }
}
