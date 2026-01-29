using CacheServerModels;
using log4net;

namespace Manager;

public class CacheEventNotifier : ICacheEventNotifier
{
    private readonly ILog _logger = LogManager.GetLogger(typeof(CacheEventNotifier));

    public event EventHandler<CacheEvent>? CacheEventOccurred;

    public void RaiseItemAdded(string key, object? value)
    {
        RaiseEvent(CacheEventType.ItemAdded, key, value);
    }

    public void RaiseItemUpdated(string key, object? value)
    {
        RaiseEvent(CacheEventType.ItemUpdated, key, value);
    }

    public void RaiseItemRemoved(string key)
    {
        RaiseEvent(CacheEventType.ItemRemoved, key, null);
    }

    public void RaiseItemExpired(string key)
    {
        RaiseEvent(CacheEventType.ItemExpired, key, null, "Item expired");
    }

    public void RaiseItemEvicted(string key, string reason)
    {
        RaiseEvent(CacheEventType.ItemEvicted, key, null, reason);
    }

    private void RaiseEvent(CacheEventType eventType, string key, object? value, string? reason = null)
    {
        var cacheEvent = new CacheEvent
        {
            EventType = eventType,
            Key = key,
            Value = value,
            Timestamp = DateTime.UtcNow,
            Reason = reason
        };

        _logger.Debug($"Raising cache event: {eventType} for key '{key}'");

        try
        {
            CacheEventOccurred?.Invoke(this, cacheEvent);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error raising cache event: {eventType} for key '{key}'", ex);
        }
    }
}
