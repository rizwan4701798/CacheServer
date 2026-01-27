
using System.Collections.Concurrent;

namespace Manager;

public class CacheManager : ICacheManager
{
    private readonly ConcurrentDictionary<string, object> _cache;
    private readonly int _maxItems;
    private readonly object _capacityLock = new();

    public CacheManager(int maxItems)
    {
        if (maxItems <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxItems),
                "Cache size must be greater than zero.");

        _maxItems = maxItems;
        _cache = new ConcurrentDictionary<string, object>();
    }

    public bool Create(string key, object value)
    {
        lock (_capacityLock)
        {
            if (_cache.Count >= _maxItems)
                throw new InvalidOperationException(CacheServerConstants.CacheIsFull);

            return _cache.TryAdd(key, value);
        }
    }

    public object Read(string key)
    {
        _cache.TryGetValue(key, out var value);
        return value;
    }

    public bool Update(string key, object value)
    {
        if (!_cache.ContainsKey(key))
            return false;

        _cache[key] = value;
        return true;
    }

    public bool Delete(string key)
    {
        return _cache.TryRemove(key, out _);
    }
}
