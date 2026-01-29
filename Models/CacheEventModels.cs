namespace CacheServerModels;

public enum CacheEventType
{
    ItemAdded,
    ItemUpdated,
    ItemRemoved,
    ItemExpired,
    ItemEvicted
}

public class CacheEvent
{
    public CacheEventType EventType { get; set; }
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Reason { get; set; }
}
