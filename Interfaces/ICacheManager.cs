namespace Manager;

/// <summary>
/// Cache manager interface defining basic cache operations.
/// </summary>
public interface ICacheManager
{
    /// <summary>
    /// Creates a new cache entry.
    /// </summary>
    /// <param name="key">The unique key for the cache entry.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expirationSeconds">Optional expiration time in seconds.</param>
    /// <returns>True if the entry was created; false if the key already exists.</returns>
    bool Create(string key, object? value, int? expirationSeconds = null);

    /// <summary>
    /// Reads a cache entry by key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The cached value, or null if not found or expired.</returns>
    object? Read(string key);

    /// <summary>
    /// Updates an existing cache entry.
    /// </summary>
    /// <param name="key">The key of the entry to update.</param>
    /// <param name="value">The new value.</param>
    /// <param name="expirationSeconds">Optional new expiration time in seconds.</param>
    /// <returns>True if the entry was updated; false if the key doesn't exist.</returns>
    bool Update(string key, object? value, int? expirationSeconds = null);

    /// <summary>
    /// Deletes a cache entry by key.
    /// </summary>
    /// <param name="key">The key of the entry to delete.</param>
    /// <returns>True if the entry was deleted; false if the key doesn't exist.</returns>
    bool Delete(string key);

    /// <summary>
    /// Gets the event notifier for cache events.
    /// </summary>
    ICacheEventNotifier EventNotifier { get; }
}

