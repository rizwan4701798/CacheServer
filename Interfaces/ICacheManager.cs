
namespace Manager;

/// <summary>
/// ECache manager interface defining basic cache operations.
/// </summary>
public interface ICacheManager
{
    /// <summary>
    /// Creates a new cache entry.
    /// </summary>
    bool Create(string key, object value, int? expirationSeconds = null);

    /// <summary>
    /// Reads a cache entry by key.
    /// </summary>
    object Read(string key);

    /// <summary>
    /// Updates an existing cache entry.
    /// </summary>
    bool Update(string key, object value, int? expirationSeconds = null);

    /// <summary>
    /// Deletes a cache entry by key.
    /// </summary>
    bool Delete(string key);

    /// <summary>
    /// Gets the event notifier for cache events.
    /// </summary>
    ICacheEventNotifier EventNotifier { get; }
}

