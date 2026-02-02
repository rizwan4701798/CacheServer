using log4net;
using Newtonsoft.Json;

namespace Manager;

public class CacheManager : ICacheManager, IDisposable
{
    private readonly ILog _logger;
    private readonly Dictionary<string, CacheItem> _cache;
    private readonly int _maxItems;
    private readonly object _lock = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    private readonly Dictionary<int, HashSet<string>> _frequencyBuckets;
    private int _minFrequency;

    public ICacheEventNotifier EventNotifier { get; }

    public CacheManager(int maxItems)
    {
        _logger = LogManager.GetLogger(typeof(CacheManager));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxItems);

        _maxItems = maxItems;
        _cache = new Dictionary<string, CacheItem>();

        _frequencyBuckets = new Dictionary<int, HashSet<string>>();
        _minFrequency = 0;

        EventNotifier = new CacheEventNotifier();

        _cleanupTimer = new Timer(
            CleanupExpiredItems,
            null,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(60));

        _logger.Info(string.Format(CacheServerConstants.CacheInitialized, _maxItems));
    }

    public bool Create(string key, object value, int? expirationSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(string.Format(CacheServerConstants.CreateFailedEmpty, SerializeValueForLog(value)));
            return false;
        }

        lock (_lock)
        {
            if (_cache.Count >= _maxItems)
            {
                _logger.Info(string.Format(CacheServerConstants.CapacityReachedLog, GetCacheStats()));
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

                _logger.Info(string.Format(CacheServerConstants.CreateSuccessLog, key, valueLog, expirationLog, GetCacheStats()));

                EventNotifier.RaiseItemAdded(key, value);
            }
            else
            {
                _logger.Warn(string.Format(CacheServerConstants.CreateDuplicateLog, key, GetCacheStats()));
            }

            return added;
        }
    }

    public object Read(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(CacheServerConstants.ReadFailedEmpty);
            return null;
        }

        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var item))
            {
                _logger.Debug(string.Format(CacheServerConstants.ReadMissLog, key, GetCacheStats()));
                return null;
            }

            if (item.IsExpired)
            {
                RemoveFromFrequencyBucket(key, item.Frequency);
                _cache.Remove(key);

                string expiredValueLog = SerializeValueForLog(item.Value);
                _logger.Info(string.Format(CacheServerConstants.ReadExpiredLog, key, expiredValueLog, item.ExpiresAt, item.Frequency, GetCacheStats()));

                EventNotifier.RaiseItemExpired(key);
                return null;
            }

            int oldFrequency = item.Frequency;
            IncrementFrequency(key, item);

            string valueLog = SerializeValueForLog(item.Value);
            string expirationLog = item.ExpiresAt.HasValue
                ? $", ExpiresAt={item.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}"
                : ", ExpiresAt=Never";

            _logger.Debug(string.Format(CacheServerConstants.ReadHitLog, key, valueLog, oldFrequency, item.Frequency, expirationLog, GetCacheStats()));

            return item.Value;
        }
    }

    public bool Update(string key, object value, int? expirationSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(string.Format(CacheServerConstants.UpdateFailedEmpty, SerializeValueForLog(value)));
            return false;
        }

        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var existingItem))
            {
                _logger.Warn(string.Format(CacheServerConstants.UpdateNotFoundLog, key, GetCacheStats()));
                return false;
            }

            if (existingItem.IsExpired)
            {
                RemoveFromFrequencyBucket(key, existingItem.Frequency);
                _cache.Remove(key);

                string expiredValueLog = SerializeValueForLog(existingItem.Value);
                _logger.Info(string.Format(CacheServerConstants.UpdateExpiredLog, key, expiredValueLog, existingItem.ExpiresAt, GetCacheStats()));

                EventNotifier.RaiseItemExpired(key);
                return false;
            }

            string oldValueLog = SerializeValueForLog(existingItem.Value);
            string newValueLog = SerializeValueForLog(value);

            existingItem.Value = value;
            existingItem.LastAccessedAt = DateTime.UtcNow;
            existingItem.ExpiresAt = expirationSeconds.HasValue
                ? DateTime.UtcNow.AddSeconds(expirationSeconds.Value)
                : existingItem.ExpiresAt;

            string expirationLog = expirationSeconds.HasValue
                ? $", ExpiresIn={expirationSeconds.Value}s, ExpiresAt={existingItem.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}"
                : (existingItem.ExpiresAt.HasValue ? $", ExpiresAt={existingItem.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}" : ", ExpiresAt=Never");

            _logger.Info(string.Format(CacheServerConstants.UpdateSuccessLog, key, oldValueLog, newValueLog, existingItem.Frequency, expirationLog, GetCacheStats()));

            EventNotifier.RaiseItemUpdated(key, value);

            return true;
        }
    }

    public bool Delete(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Warn(CacheServerConstants.DeleteFailedEmpty);
            return false;
        }

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var item))
            {
                RemoveFromFrequencyBucket(key, item.Frequency);

                string valueLog = SerializeValueForLog(item.Value);
                string expirationLog = item.ExpiresAt.HasValue
                    ? $", ExpiresAt={item.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}"
                    : ", ExpiresAt=Never";

                _cache.Remove(key);

                _logger.Info(string.Format(CacheServerConstants.DeleteSuccessLog, key, valueLog, item.Frequency, expirationLog, GetCacheStats()));

                EventNotifier.RaiseItemRemoved(key);
                return true;
            }

            _logger.Warn(string.Format(CacheServerConstants.DeleteNotFoundLog, key, GetCacheStats()));
            return false;
        }
    }

    private void CleanupExpiredItems(object state)
    {
        lock (_lock)
        {
            var expiredItems = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .ToList();

            foreach (var kvp in expiredItems)
            {
                RemoveFromFrequencyBucket(kvp.Key, kvp.Value.Frequency);
                _cache.Remove(kvp.Key);

                string valueLog = SerializeValueForLog(kvp.Value.Value);
                _logger.Info(string.Format(CacheServerConstants.CleanupExpiredLog, kvp.Key, valueLog, kvp.Value.ExpiresAt, kvp.Value.Frequency));

                EventNotifier.RaiseItemExpired(kvp.Key);
            }

            if (expiredItems.Count > 0)
            {
                _logger.Info(string.Format(CacheServerConstants.CleanupCompleteLog, expiredItems.Count, GetCacheStats()));
            }
        }
    }

    #region Logging Helper Methods

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
            bucket = [];
            _frequencyBuckets[frequency] = bucket;
        }

        bucket.Add(key);
    }

    private void RemoveFromFrequencyBucket(string key, int frequency)
    {
        if (_frequencyBuckets.TryGetValue(frequency, out var bucket))
        {
            bucket.Remove(key);

            if (bucket.Count == 0)
            {
                _frequencyBuckets.Remove(frequency);
            }
        }
    }

    private void IncrementFrequency(string key, CacheItem item)
    {
        int oldFreq = item.Frequency;
        int newFreq = oldFreq + 1;

        RemoveFromFrequencyBucket(key, oldFreq);

        if (oldFreq == _minFrequency && !_frequencyBuckets.ContainsKey(oldFreq))
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
            string keyToEvict = bucket.First();

            if (_cache.TryGetValue(keyToEvict, out var itemToEvict))
            {
                string valueLog = SerializeValueForLog(itemToEvict.Value);
                string expirationLog = itemToEvict.ExpiresAt.HasValue
                    ? $", ExpiresAt={itemToEvict.ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}"
                    : ", ExpiresAt=Never";

                RemoveFromFrequencyBucket(keyToEvict, _minFrequency);
                _cache.Remove(keyToEvict);

                _logger.Info(string.Format(CacheServerConstants.LfuEvictionLog, keyToEvict, valueLog, _minFrequency, expirationLog, GetCacheStats()));

                EventNotifier.RaiseItemEvicted(keyToEvict, string.Format(CacheServerConstants.LfuEvictionStart, _minFrequency));
            }
            else
            {
                RemoveFromFrequencyBucket(keyToEvict, _minFrequency);
                _logger.Warn(string.Format(CacheServerConstants.LfuEvictionNotFoundLog, keyToEvict));
            }

            if (!_frequencyBuckets.ContainsKey(_minFrequency))
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
            _logger.Info(CacheServerConstants.CacheManagerDisposed);
        }

        _disposed = true;
    }

    #endregion
}
