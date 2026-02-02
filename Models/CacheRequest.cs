namespace CacheServerModels;

public enum CacheOperation
{
    Get,
    Set,
    Delete,
    Subscribe,
    Unsubscribe
}

public class CacheRequest
{
    public virtual CacheOperation Operation { get; set; }
    public string? Key { get; set; }
    public object? Value { get; set; }
    public int? ExpirationSeconds { get; set; }
    public string[]? SubscribedEventTypes { get; set; }
}

public class BasicRequest : CacheRequest
{
    public override CacheOperation Operation { get; set; }

    public BasicRequest() { }
    
    public BasicRequest(CacheOperation operation) 
    {
        Operation = operation;
    }
}

public class KeyRequest : CacheRequest
{
    public override CacheOperation Operation { get; set; }
    public string Key { get; set; } = string.Empty;

    public KeyRequest() { }
    public KeyRequest(CacheOperation operation, string key) 
    {
        Operation = operation;
        Key = key;
    }
}
