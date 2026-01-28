using System.Collections.Concurrent;
using log4net;

namespace Manager;

internal class CacheItem
{
    public object Value { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}

public class CacheManager : ICacheManager
{
    private readonly ILog _logger;
    private readonly ConcurrentDictionary<string, CacheItem> _cache;
    private readonly int _maxItems;
    private readonly object _capacityLock = new();
    private readonly Timer _cleanupTimer;

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

            var item = new CacheItem
            {
                Value = value,
                ExpiresAt = expirationSeconds.HasValue
                    ? DateTime.UtcNow.AddSeconds(expirationSeconds.Value)
                    : null
            };

            bool added = _cache.TryAdd(key, item);

            if (added)
            {
                _logger.Info(
                    string.Format(
                        CacheServerConstants.CreateSuccess,
                        key));

                if (expirationSeconds.HasValue)
                {
                    _logger.Debug($"Key '{key}' will expire in {expirationSeconds.Value} seconds.");
                }
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
            // Lazy expiration check
            if (item.IsExpired)
            {
                _cache.TryRemove(key, out _);
                _logger.Debug($"Key '{key}' expired and removed on read.");
                return null;
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

        if (!_cache.TryGetValue(key, out var existingItem))
        {
            _logger.Warn(
                string.Format(
                    CacheServerConstants.UpdateMissingKey,
                    key));
            return false;
        }

        // Check if expired
        if (existingItem.IsExpired)
        {
            _cache.TryRemove(key, out _);
            _logger.Debug($"Key '{key}' expired and removed on update attempt.");
            return false;
        }

        var newItem = new CacheItem
        {
            Value = value,
            ExpiresAt = expirationSeconds.HasValue
                ? DateTime.UtcNow.AddSeconds(expirationSeconds.Value)
                : existingItem.ExpiresAt  // Keep existing expiration if not specified
        };

        _cache[key] = newItem;

        _logger.Info(
            string.Format(
                CacheServerConstants.UpdateSuccess,
                key));

        if (expirationSeconds.HasValue)
        {
            _logger.Debug($"Key '{key}' expiration updated to {expirationSeconds.Value} seconds.");
        }

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

    private void CleanupExpiredItems(object state)
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
            _logger.Debug($"Expired item '{key}' cleaned up by background task.");
        }

        if (expiredKeys.Count > 0)
        {
            _logger.Info($"Background cleanup removed {expiredKeys.Count} expired item(s).");
        }
    }
}
