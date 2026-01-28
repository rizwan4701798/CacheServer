using System.Collections.Concurrent;
using log4net;
using Newtonsoft.Json;

namespace Manager;

internal sealed class CacheItem
{
    public object? Value { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int Frequency { get; set; }
    public DateTime LastAccessedAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}

public sealed class CacheManager : ICacheManager, IDisposable
{
    private readonly ILog _logger;
    private readonly ConcurrentDictionary<string, CacheItem> _cache;
    private readonly int _maxItems;
    private readonly object _capacityLock = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    // LFU tracking: frequency -> set of keys with that frequency (ordered by access time)
    private readonly SortedDictionary<int, LinkedList<string>> _frequencyBuckets;
    // Quick lookup: key -> node in the linked list (for O(1) removal)
    private readonly Dictionary<string, LinkedListNode<string>> _keyNodes;
    private int _minFrequency;

    // Event notifier for cache events
    public ICacheEventNotifier EventNotifier { get; }

    public CacheManager(int maxItems)
    {
        _logger = LogManager.GetLogger(typeof(CacheManager));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxItems);

        _maxItems = maxItems;
        _cache = new ConcurrentDictionary<string, CacheItem>();

        // Initialize LFU tracking
        _frequencyBuckets = new SortedDictionary<int, LinkedList<string>>();
        _keyNodes = new Dictionary<string, LinkedListNode<string>>();
        _minFrequency = 0;

        // Initialize event notifier
        EventNotifier = new CacheEventNotifier();

        // Background cleanup every 60 seconds
        _cleanupTimer = new Timer(
            CleanupExpiredItems,
            null,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(60));

        _logger.Info($"CacheManager initialized with capacity {_maxItems}");
    }

    public bool Create(string key, object value, int? expirationSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn($"CREATE FAILED: Empty or null key provided, Value={SerializeValueForLog(value)}");
            return false;
        }

        lock (_capacityLock)
        {
            // If at capacity, evict least frequently used item
            if (_cache.Count >= _maxItems)
            {
                _logger.Info($"CAPACITY REACHED: Cache full, triggering LFU eviction {GetCacheStats()}");
                EvictLeastFrequentlyUsed();
            }

            var item = new CacheItem
            {
                Value = value,
                ExpiresAt = expirationSeconds.HasValue
                    ? DateTime.UtcNow.AddSeconds(expirationSeconds.Value)
                    : null,
                Frequency = 1,
                LastAccessedAt = DateTime.UtcNow
            };

            bool added = _cache.TryAdd(key, item);

            if (added)
            {
                AddToFrequencyBucket(key, 1);
                _minFrequency = 1;

                string valueLog = SerializeValueForLog(value);
                string expirationLog = expirationSeconds.HasValue
                    ? $", ExpiresIn={expirationSeconds.Value}s, ExpiresAt={item.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}"
                    : ", ExpiresIn=Never";

                _logger.Info($"CREATE SUCCESS: Key='{key}', Value={valueLog}{expirationLog} {GetCacheStats()}");

                EventNotifier.RaiseItemAdded(key, value);
            }
            else
            {
                _logger.Warn($"CREATE FAILED (duplicate): Key='{key}' already exists {GetCacheStats()}");
            }

            return added;
        }
    }

    public object Read(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn("READ FAILED: Empty or null key provided");
            return null;
        }

        lock (_capacityLock)
        {
            if (!_cache.TryGetValue(key, out var item))
            {
                _logger.Debug($"READ MISS: Key='{key}' not found {GetCacheStats()}");
                return null;
            }

            if (item.IsExpired)
            {
                RemoveFromFrequencyBucket(key, item.Frequency);
                _cache.TryRemove(key, out _);

                string expiredValueLog = SerializeValueForLog(item.Value);
                _logger.Info($"READ EXPIRED: Key='{key}', Value={expiredValueLog}, ExpiredAt={item.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}, Frequency={item.Frequency} {GetCacheStats()}");

                EventNotifier.RaiseItemExpired(key);
                return null;
            }

            int oldFrequency = item.Frequency;
            IncrementFrequency(key, item);

            string valueLog = SerializeValueForLog(item.Value);
            string expirationLog = item.ExpiresAt.HasValue
                ? $", ExpiresAt={item.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}"
                : ", ExpiresAt=Never";

            _logger.Debug($"READ HIT: Key='{key}', Value={valueLog}, Frequency={oldFrequency}->{item.Frequency}{expirationLog} {GetCacheStats()}");

            return item.Value;
        }
    }

    public bool Update(string key, object value, int? expirationSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn($"UPDATE FAILED: Empty or null key provided, Value={SerializeValueForLog(value)}");
            return false;
        }

        lock (_capacityLock)
        {
            if (!_cache.TryGetValue(key, out var existingItem))
            {
                _logger.Warn($"UPDATE FAILED: Key='{key}' not found {GetCacheStats()}");
                return false;
            }

            if (existingItem.IsExpired)
            {
                RemoveFromFrequencyBucket(key, existingItem.Frequency);
                _cache.TryRemove(key, out _);

                string expiredValueLog = SerializeValueForLog(existingItem.Value);
                _logger.Info($"UPDATE EXPIRED: Key='{key}', OldValue={expiredValueLog}, ExpiredAt={existingItem.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC} {GetCacheStats()}");

                EventNotifier.RaiseItemExpired(key);
                return false;
            }

            string oldValueLog = SerializeValueForLog(existingItem.Value);
            string newValueLog = SerializeValueForLog(value);
            DateTime? oldExpiration = existingItem.ExpiresAt;

            existingItem.Value = value;
            existingItem.LastAccessedAt = DateTime.UtcNow;
            existingItem.ExpiresAt = expirationSeconds.HasValue
                ? DateTime.UtcNow.AddSeconds(expirationSeconds.Value)
                : existingItem.ExpiresAt;

            string expirationLog = expirationSeconds.HasValue
                ? $", ExpiresIn={expirationSeconds.Value}s, ExpiresAt={existingItem.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}"
                : (existingItem.ExpiresAt.HasValue ? $", ExpiresAt={existingItem.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}" : ", ExpiresAt=Never");

            _logger.Info($"UPDATE SUCCESS: Key='{key}', OldValue={oldValueLog}, NewValue={newValueLog}, Frequency={existingItem.Frequency}{expirationLog} {GetCacheStats()}");

            EventNotifier.RaiseItemUpdated(key, value);

            return true;
        }
    }

    public bool Delete(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn("DELETE FAILED: Empty or null key provided");
            return false;
        }

        lock (_capacityLock)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                RemoveFromFrequencyBucket(key, item.Frequency);

                string valueLog = SerializeValueForLog(item.Value);
                string expirationLog = item.ExpiresAt.HasValue
                    ? $", ExpiresAt={item.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}"
                    : ", ExpiresAt=Never";

                _cache.TryRemove(key, out _);

                _logger.Info($"DELETE SUCCESS: Key='{key}', Value={valueLog}, Frequency={item.Frequency}{expirationLog} {GetCacheStats()}");

                EventNotifier.RaiseItemRemoved(key);
                return true;
            }

            _logger.Warn($"DELETE FAILED: Key='{key}' not found {GetCacheStats()}");
            return false;
        }
    }

    private void CleanupExpiredItems(object state)
    {
        lock (_capacityLock)
        {
            var expiredItems = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .ToList();

            foreach (var kvp in expiredItems)
            {
                RemoveFromFrequencyBucket(kvp.Key, kvp.Value.Frequency);
                _cache.TryRemove(kvp.Key, out _);

                string valueLog = SerializeValueForLog(kvp.Value.Value);
                _logger.Info($"CLEANUP EXPIRED: Key='{kvp.Key}', Value={valueLog}, ExpiredAt={kvp.Value.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}, Frequency={kvp.Value.Frequency}");

                EventNotifier.RaiseItemExpired(kvp.Key);
            }

            if (expiredItems.Count > 0)
            {
                _logger.Info($"CLEANUP COMPLETE: Removed {expiredItems.Count} expired item(s) {GetCacheStats()}");
            }
        }
    }

    #region Logging Helper Methods

    /// <summary>
    /// Safely serializes a value for logging, truncating if too long.
    /// </summary>
    private string SerializeValueForLog(object value, int maxLength = 200)
    {
        if (value == null) return "null";

        try
        {
            string serialized = JsonConvert.SerializeObject(value);
            if (serialized.Length > maxLength)
            {
                return serialized.Substring(0, maxLength) + "... [truncated]";
            }
            return serialized;
        }
        catch
        {
            return value.GetType().Name;
        }
    }

    /// <summary>
    /// Gets the current cache statistics for logging.
    /// </summary>
    private string GetCacheStats()
    {
        return $"[CacheCount={_cache.Count}/{_maxItems}]";
    }

    #endregion

    #region LFU Helper Methods

    private void AddToFrequencyBucket(string key, int frequency)
    {
        if (!_frequencyBuckets.TryGetValue(frequency, out var bucket))
        {
            bucket = new LinkedList<string>();
            _frequencyBuckets[frequency] = bucket;
        }

        var node = bucket.AddLast(key);
        _keyNodes[key] = node;
    }

    private void RemoveFromFrequencyBucket(string key, int frequency)
    {
        if (_keyNodes.TryGetValue(key, out var node))
        {
            if (_frequencyBuckets.TryGetValue(frequency, out var bucket))
            {
                bucket.Remove(node);

                if (bucket.Count == 0)
                {
                    _frequencyBuckets.Remove(frequency);
                }
            }
            _keyNodes.Remove(key);
        }
    }

    private void IncrementFrequency(string key, CacheItem item)
    {
        int oldFreq = item.Frequency;
        int newFreq = oldFreq + 1;

        RemoveFromFrequencyBucket(key, oldFreq);

        if (oldFreq == _minFrequency &&
            (!_frequencyBuckets.ContainsKey(oldFreq) || _frequencyBuckets[oldFreq].Count == 0))
        {
            _minFrequency = newFreq;
        }

        AddToFrequencyBucket(key, newFreq);

        item.Frequency = newFreq;
        item.LastAccessedAt = DateTime.UtcNow;
    }

    private void EvictLeastFrequentlyUsed()
    {
        if (_frequencyBuckets.TryGetValue(_minFrequency, out var bucket) && bucket.Count > 0)
        {
            string keyToEvict = bucket.First.Value;

            if (_cache.TryGetValue(keyToEvict, out var itemToEvict))
            {
                string valueLog = SerializeValueForLog(itemToEvict.Value);
                string expirationLog = itemToEvict.ExpiresAt.HasValue
                    ? $", ExpiresAt={itemToEvict.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}"
                    : ", ExpiresAt=Never";

                RemoveFromFrequencyBucket(keyToEvict, _minFrequency);
                _cache.TryRemove(keyToEvict, out _);

                _logger.Info($"LFU EVICTION: Key='{keyToEvict}', Value={valueLog}, Frequency={_minFrequency}{expirationLog} {GetCacheStats()}");

                EventNotifier.RaiseItemEvicted(keyToEvict, $"LFU eviction (frequency: {_minFrequency})");
            }
            else
            {
                RemoveFromFrequencyBucket(keyToEvict, _minFrequency);
                _logger.Warn($"LFU EVICTION: Key='{keyToEvict}' was in frequency bucket but not in cache");
            }

            if (!_frequencyBuckets.ContainsKey(_minFrequency) || _frequencyBuckets[_minFrequency].Count == 0)
            {
                _minFrequency = _frequencyBuckets.Count > 0 ? _frequencyBuckets.Keys.Min() : 0;
            }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _cleanupTimer?.Dispose();
            _logger.Info("CacheManager disposed");
        }

        _disposed = true;
    }

    #endregion
}
