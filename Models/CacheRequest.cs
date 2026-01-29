namespace CacheServerModels;

public class CacheRequest
{
    public string? Operation { get; set; }
    public string? Key { get; set; }
    public object? Value { get; set; }
    public int? ExpirationSeconds { get; set; }
    public string[]? SubscribedEventTypes { get; set; }
    public string? KeyPattern { get; set; }
}

