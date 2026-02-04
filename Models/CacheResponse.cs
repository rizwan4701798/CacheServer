namespace CacheServerModels;

public class CacheResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool IsNotification { get; set; }
}

public class SuccessResponse : CacheResponse
{
    public SuccessResponse()
    {
        Success = true;
    }
}

public class DataResponse : SuccessResponse
{
    public object? Value { get; set; }

    public DataResponse(object? value)
    {
        Value = value;
    }
}

public class ErrorResponse : CacheResponse
{
    public ErrorResponse(string error)
    {
        Success = false;
        Error = error;
    }
}

public class NotificationResponse : CacheResponse
{
    public CacheEvent? Event { get; set; }

    public NotificationResponse(CacheEvent cacheEvent)
    {
        IsNotification = true;
        Event = cacheEvent;
        Success = true;
    }
}
