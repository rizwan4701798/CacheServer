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
    event EventHandler<CacheEvent> CacheEventOccurred;

    /// <summary>
    /// Raises an event when an item is added to the cache.
    /// </summary>
    void RaiseItemAdded(string key, object value);

    /// <summary>
    /// Raises an event when an item is updated in the cache.
    /// </summary>
    void RaiseItemUpdated(string key, object value);

    /// <summary>
    /// Raises an event when an item is removed from the cache.
    /// </summary>
    void RaiseItemRemoved(string key);

    /// <summary>
    /// Raises an event when an item expires in the cache.
    /// </summary>
    void RaiseItemExpired(string key);

    /// <summary>
    /// Raises an event when an item is evicted from the cache.
    /// </summary>
    void RaiseItemEvicted(string key, string reason);
}
