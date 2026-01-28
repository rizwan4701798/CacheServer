using System.Collections.Concurrent;
using log4net;

namespace Manager;

internal class CacheItem
{
    public object Value { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int Frequency { get; set; }
    public DateTime LastAccessedAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}

public class CacheManager : ICacheManager
{
    private readonly ILog _logger;
    private readonly ConcurrentDictionary<string, CacheItem> _cache;
    private readonly int _maxItems;
    private readonly object _capacityLock = new();
    private readonly Timer _cleanupTimer;

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

        if (maxItems <= 0)
        {
            _logger.Error($"{CacheServerConstants.InvalidCacheSize}: {maxItems}");
            throw new ArgumentOutOfRangeException(
                nameof(maxItems),
                CacheServerConstants.CacheSizeMustBeGreaterThanZero);
        }

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

        _logger.Info(
            string.Format(
                CacheServerConstants.CacheInitialized,
                _maxItems));
    }

    public bool Create(string key, object value, int? expirationSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(CacheServerConstants.CreateEmptyKey);
            return false;
        }

        lock (_capacityLock)
        {
            // If at capacity, evict least frequently used item
            if (_cache.Count >= _maxItems)
            {
                _logger.Debug(
                    string.Format(
                        CacheServerConstants.CacheCapacityReached,
                        _maxItems,
                        _cache.Count));

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

                _logger.Info(
                    string.Format(
                        CacheServerConstants.CreateSuccess,
                        key));

                if (expirationSeconds.HasValue)
                {
                    _logger.Debug($"Key '{key}' will expire in {expirationSeconds.Value} seconds.");
                }

                EventNotifier.RaiseItemAdded(key, value);
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

        if (_cache.TryGetValue(key, out var item))
        {
            lock (_capacityLock)
            {
                if (item.IsExpired)
                {
                    RemoveFromFrequencyBucket(key, item.Frequency);
                    _cache.TryRemove(key, out _);
                    _logger.Debug($"Key '{key}' expired and removed on read.");

                    EventNotifier.RaiseItemExpired(key);
                    return null;
                }

                IncrementFrequency(key, item);
            }

            _logger.Debug(
                string.Format(
                    CacheServerConstants.CacheHit,
                    key));
            return item.Value;
        }

        _logger.Debug(
            string.Format(
                CacheServerConstants.CacheMiss,
                key));

        return null;
    }

    public bool Update(string key, object value, int? expirationSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(CacheServerConstants.UpdateEmptyKey);
            return false;
        }

        lock (_capacityLock)
        {
            if (!_cache.TryGetValue(key, out var existingItem))
            {
                _logger.Warn(
                    string.Format(
                        CacheServerConstants.UpdateMissingKey,
                        key));
                return false;
            }

            if (existingItem.IsExpired)
            {
                RemoveFromFrequencyBucket(key, existingItem.Frequency);
                _cache.TryRemove(key, out _);
                _logger.Debug($"Key '{key}' expired and removed on update attempt.");

                EventNotifier.RaiseItemExpired(key);
                return false;
            }

            existingItem.Value = value;
            existingItem.LastAccessedAt = DateTime.UtcNow;
            existingItem.ExpiresAt = expirationSeconds.HasValue
                ? DateTime.UtcNow.AddSeconds(expirationSeconds.Value)
                : existingItem.ExpiresAt;

            _logger.Info(
                string.Format(
                    CacheServerConstants.UpdateSuccess,
                    key));

            if (expirationSeconds.HasValue)
            {
                _logger.Debug($"Key '{key}' expiration updated to {expirationSeconds.Value} seconds.");
            }

            EventNotifier.RaiseItemUpdated(key, value);

            return true;
        }
    }

    public bool Delete(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(CacheServerConstants.DeleteEmptyKey);
            return false;
        }

        lock (_capacityLock)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                RemoveFromFrequencyBucket(key, item.Frequency);
            }

            bool removed = _cache.TryRemove(key, out _);

            if (removed)
            {
                _logger.Info(
                    string.Format(
                        CacheServerConstants.DeleteSuccess,
                        key));

                EventNotifier.RaiseItemRemoved(key);
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
                _logger.Debug($"Expired item '{kvp.Key}' cleaned up by background task.");

                EventNotifier.RaiseItemExpired(kvp.Key);
            }

            if (expiredItems.Count > 0)
            {
                _logger.Info($"Background cleanup removed {expiredItems.Count} expired item(s).");
            }
        }
    }

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

            RemoveFromFrequencyBucket(keyToEvict, _minFrequency);

            _cache.TryRemove(keyToEvict, out _);

            _logger.Info($"LFU eviction: removed key '{keyToEvict}' with frequency {_minFrequency}.");

            EventNotifier.RaiseItemEvicted(keyToEvict, $"LFU eviction (frequency: {_minFrequency})");

            if (!_frequencyBuckets.ContainsKey(_minFrequency) || _frequencyBuckets[_minFrequency].Count == 0)
            {
                _minFrequency = _frequencyBuckets.Count > 0 ? _frequencyBuckets.Keys.Min() : 0;
            }
        }
    }

    #endregion
}
