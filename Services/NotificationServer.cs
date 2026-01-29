using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CacheServerModels;
using log4net;
using Manager;
using Newtonsoft.Json;

namespace CacheServer.Server;

public class NotificationServer
{
    private readonly TcpListener _listener;
    private readonly ICacheManager _cacheManager;
    private readonly ConcurrentDictionary<string, NotificationClient> _subscribers = new();
    private readonly ILog _logger = LogManager.GetLogger(typeof(NotificationServer));
    private volatile bool _isRunning;

    public NotificationServer(int port, ICacheManager cacheManager)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(cacheManager);

        _listener = new TcpListener(IPAddress.Any, port);
        _cacheManager = cacheManager;

        _cacheManager.EventNotifier.CacheEventOccurred += OnCacheEvent;
    }

    public void Start()
    {
        _isRunning = true;
        _listener.Start();
        _logger.Info($"Notification server started on port {((IPEndPoint)_listener.LocalEndpoint).Port}.");

        _ = Task.Run(AcceptClientsAsync);
    }

    public void Stop()
    {
        _isRunning = false;
        _listener.Stop();

        foreach (var client in _subscribers.Values)
        {
            try
            {
                client.TcpClient.Close();
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error closing client connection: {ex.Message}");
            }
        }
        _subscribers.Clear();

        _logger.Info("Notification server stopped.");
    }

    private async Task AcceptClientsAsync()
    {
        while (_isRunning)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                var clientId = Guid.NewGuid().ToString();
                var notificationClient = new NotificationClient(clientId, tcpClient);

                _subscribers.TryAdd(clientId, notificationClient);
                _logger.Info($"Notification client connected: {clientId}");

                _ = HandleClientAsync(notificationClient);
            }
            catch (Exception ex)
            {
                if (_isRunning)
                    _logger.Error("Error accepting notification client", ex);
            }
        }
    }

    private async Task HandleClientAsync(NotificationClient client)
    {
        try
        {
            var stream = client.TcpClient.GetStream();
            var buffer = new byte[4096];

            while (client.TcpClient.Connected && _isRunning)
            {
                int bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (bytesRead == 0) break;

                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var request = JsonConvert.DeserializeObject<CacheRequest>(json);

                if (request?.Operation?.ToUpperInvariant() == "SUBSCRIBE")
                {
                    client.SubscribedEvents = request.SubscribedEventTypes?
                        .Select(e => Enum.Parse<CacheEventType>(e))
                        .ToHashSet() ?? Enum.GetValues<CacheEventType>().ToHashSet();

                    _logger.Info($"Client {client.Id} subscribed to events: {string.Join(", ", client.SubscribedEvents)}");

                    var ack = new CacheResponse { Success = true };
                    await SendToClientAsync(client, ack).ConfigureAwait(false);
                }
                else if (request?.Operation?.ToUpperInvariant() == "UNSUBSCRIBE")
                {
                    _logger.Info($"Client {client.Id} unsubscribed.");
                    _subscribers.TryRemove(client.Id, out _);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            if (_isRunning)
                _logger.Debug($"Notification client {client.Id} disconnected: {ex.Message}");
        }
        finally
        {
            _subscribers.TryRemove(client.Id, out _);
            try
            {
                client.TcpClient.Close();
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error closing client {client.Id}: {ex.Message}");
            }
            _logger.Debug($"Notification client {client.Id} removed.");
        }
    }

    private void OnCacheEvent(object? sender, CacheEvent cacheEvent)
    {
        var notification = new CacheResponse
        {
            IsNotification = true,
            Event = cacheEvent,
            Success = true
        };

        var clientsToRemove = new List<string>();

        foreach (var kvp in _subscribers)
        {
            var client = kvp.Value;

            if (client.SubscribedEvents.Count > 0 && !client.SubscribedEvents.Contains(cacheEvent.EventType))
                continue;

            try
            {
                SendToClient(client, notification);
            }
            catch (Exception ex)
            {
                _logger.Debug($"Failed to send notification to client {client.Id}: {ex.Message}");
                clientsToRemove.Add(kvp.Key);
            }
        }

        foreach (var clientId in clientsToRemove)
        {
            if (_subscribers.TryRemove(clientId, out var client))
            {
                try
                {
                    client.TcpClient.Close();
                }
                catch (Exception ex)
                {
                    _logger.Debug($"Error closing client {clientId}: {ex.Message}");
                }
            }
        }
    }

    private void SendToClient(NotificationClient client, CacheResponse response)
    {
        if (!client.TcpClient.Connected) return;

        var json = JsonConvert.SerializeObject(response);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");

        lock (client.WriteLock)
        {
            var stream = client.TcpClient.GetStream();
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
    }

    private async Task SendToClientAsync(NotificationClient client, CacheResponse response)
    {
        if (!client.TcpClient.Connected) return;

        var json = JsonConvert.SerializeObject(response);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");

        var stream = client.TcpClient.GetStream();
        await stream.WriteAsync(bytes).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    public int SubscriberCount => _subscribers.Count;
}

internal sealed class NotificationClient(string id, TcpClient tcpClient)
{
    public string Id { get; } = id;
    public TcpClient TcpClient { get; } = tcpClient;
    public HashSet<CacheEventType> SubscribedEvents { get; set; } = [];
    public object WriteLock { get; } = new();
}
