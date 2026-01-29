namespace CacheServerModels;

public class CacheResponse
{
    public bool Success { get; set; }
    public object? Value { get; set; }
    public string? Error { get; set; }
    public bool IsNotification { get; set; }
    public CacheEvent? Event { get; set; }
}
