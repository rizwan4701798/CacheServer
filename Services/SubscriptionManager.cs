using System.Collections.Concurrent;
using CacheServerModels;
using Manager;

namespace CacheServer.Services;

public interface ISubscriptionManager
{
    void AddClient(string clientId, IClientSession session);
    void RemoveClient(string clientId);
    void Subscribe(string clientId, IEnumerable<CacheEventType> eventTypes);
    void Unsubscribe(string clientId);
}

public class SubscriptionManager : ISubscriptionManager
{
    private readonly ConcurrentDictionary<string, ClientSubscription> _subscribers = new();

    public SubscriptionManager(ICacheManager cacheManager)
    {
        cacheManager.EventNotifier.CacheEventOccurred += OnCacheEvent;
    }

    public void AddClient(string clientId, IClientSession session)
    {
        _subscribers.TryAdd(clientId, new ClientSubscription(session));
    }

    public void RemoveClient(string clientId)
    {
        _subscribers.TryRemove(clientId, out _);
    }

    public void Subscribe(string clientId, IEnumerable<CacheEventType> eventTypes)
    {
        if (_subscribers.TryGetValue(clientId, out var subscription))
        {
            var newSet = new HashSet<CacheEventType>(eventTypes);
            subscription.SubscribedEvents = newSet;
        }
    }

    public void Unsubscribe(string clientId)
    {
        if (_subscribers.TryGetValue(clientId, out var subscription))
        {
            subscription.SubscribedEvents.Clear();
        }
    }

    private void OnCacheEvent(object? sender, CacheEvent cacheEvent)
    {
        var notification = new NotificationResponse(cacheEvent);

        foreach (var sub in _subscribers.Values)
        {
            if (sub.SubscribedEvents.Count > 0 && 
                (sub.SubscribedEvents.Contains(cacheEvent.EventType)))
            {
                _ = sub.Session.SendAsync(notification);
            }
        }
    }

    private class ClientSubscription
    {
        public IClientSession Session { get; }
        public HashSet<CacheEventType> SubscribedEvents { get; set; } = new();

        public ClientSubscription(IClientSession session)
        {
            Session = session;
        }
    }
}
