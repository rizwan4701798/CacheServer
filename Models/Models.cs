namespace CacheServerModels;

public sealed class CacheRequest
{
    public string? Operation { get; set; }
    public string? Key { get; set; }
    public object? Value { get; set; }
    public int? ExpirationSeconds { get; set; }
    public string[]? SubscribedEventTypes { get; set; }
    public string? KeyPattern { get; set; }
}

public sealed class CacheResponse
{
    public bool Success { get; set; }
    public object? Value { get; set; }
    public string? Error { get; set; }
    public bool IsNotification { get; set; }
    public CacheEvent? Event { get; set; }
}
