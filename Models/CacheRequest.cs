using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CacheServerModels;

[JsonConverter(typeof(CacheRequestConverter))]
public abstract class CacheRequest
{
    public virtual CacheOperation Operation { get; set; }
}

public class BasicRequest : CacheRequest
{
    public BasicRequest() { }
    
    public BasicRequest(CacheOperation operation) 
    {
        Operation = operation;
    }
}

public class KeyRequest : CacheRequest
{
    public string Key { get; set; } = string.Empty;

    public KeyRequest() { }
    public KeyRequest(CacheOperation operation, string key) 
    {
        Operation = operation;
        Key = key;
    }
}

public class DataRequest : KeyRequest
{
    public object? Value { get; set; }
    public int? ExpirationSeconds { get; set; }

    public DataRequest() { }
    public DataRequest(CacheOperation operation, string key, object? value, int? expirationSeconds) 
        : base(operation, key)
    {
        Value = value;
        ExpirationSeconds = expirationSeconds;
    }
}

public class SubscriptionRequest : CacheRequest
{
    public string[]? SubscribedEventTypes { get; set; }
    
    public SubscriptionRequest() { }
    public SubscriptionRequest(string[]? eventTypes)
    {
        Operation = CacheOperation.Subscribe;
        SubscribedEventTypes = eventTypes;
    }
    public SubscriptionRequest(CacheOperation operation, string[]? eventTypes)
    {
        Operation = operation;
        SubscribedEventTypes = eventTypes;
    }
}


public class CacheRequestConverter : JsonConverter
{
    public override bool CanWrite => false;
    public override bool CanRead => true;
    public override bool CanConvert(Type? objectType) => objectType == typeof(CacheRequest);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;

        var jsonObject = JObject.Load(reader);
        var operationToken = jsonObject["Operation"];
        
        if (operationToken == null || operationToken.Type == JTokenType.Null) 
            throw new JsonSerializationException("Missing Operation field");
        
        CacheOperation op;
        try 
        {
             op = operationToken.ToObject<CacheOperation>(serializer);
        }
        catch
        {
             throw new JsonSerializationException($"Invalid Operation: {operationToken}");
        }

        CacheRequest request = op switch
        {
            CacheOperation.Create or CacheOperation.Update => new DataRequest(),
            CacheOperation.Read or CacheOperation.Delete => new KeyRequest(),
            CacheOperation.Subscribe or CacheOperation.Unsubscribe => new SubscriptionRequest(),
            _ => new BasicRequest() 
        };
        
        request.Operation = op;

        using (var subReader = jsonObject.CreateReader())
        {
            serializer.Populate(subReader, request);
        }

        return request;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) 
    {
        throw new NotImplementedException();
    }
}
