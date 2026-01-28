using CacheServerModels;

namespace Manager;

/// <summary>
/// Interface for raising cache events.
/// </summary>
public interface ICacheEventNotifier
{
    /// <summary>
    /// Event raised when any cache event occurs.
    /// </summary>
    event EventHandler<CacheEvent>? CacheEventOccurred;

    /// <summary>
    /// Raises an event when an item is added to the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value being added.</param>
    void RaiseItemAdded(string key, object? value);

    /// <summary>
    /// Raises an event when an item is updated in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The new value.</param>
    void RaiseItemUpdated(string key, object? value);

    /// <summary>
    /// Raises an event when an item is removed from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    void RaiseItemRemoved(string key);

    /// <summary>
    /// Raises an event when an item expires in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    void RaiseItemExpired(string key);

    /// <summary>
    /// Raises an event when an item is evicted from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="reason">The reason for eviction.</param>
    void RaiseItemEvicted(string key, string reason);
}
