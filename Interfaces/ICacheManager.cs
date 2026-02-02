namespace Manager;

public interface ICacheManager
{
    bool Create(string key, object? value, int? expirationSeconds = null);

    object? Read(string key);

    bool Update(string key, object? value, int? expirationSeconds = null);

    bool Delete(string key);

    ICacheEventNotifier EventNotifier { get; }
}

