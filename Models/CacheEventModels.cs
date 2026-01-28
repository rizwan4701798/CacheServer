namespace CacheServerModels;

/// <summary>
/// Types of cache events that can be raised.
/// </summary>
public enum CacheEventType
{
    ItemAdded,
    ItemUpdated,
    ItemRemoved,
    ItemExpired,
    ItemEvicted
}

/// <summary>
/// Represents a cache event notification.
/// </summary>
public class CacheEvent
{
    public CacheEventType EventType { get; set; }
    public string Key { get; set; }
    public object Value { get; set; }
    public DateTime Timestamp { get; set; }
    public string Reason { get; set; }
}
