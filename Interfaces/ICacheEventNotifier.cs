using CacheServerModels;

namespace Manager;

public interface ICacheEventNotifier
{
    event EventHandler<CacheEvent>? CacheEventOccurred;

    void RaiseItemAdded(string key, object? value);

    void RaiseItemUpdated(string key, object? value);

    void RaiseItemRemoved(string key);

    void RaiseItemExpired(string key);

    void RaiseItemEvicted(string key, string reason);
}
