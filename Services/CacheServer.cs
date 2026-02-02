using log4net;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Manager;
using CacheServerModels;

namespace CacheServer.Server;

public class CacheServer
{
    private readonly TcpListener _listener;
    private readonly ICacheManager _cacheManager;
    private readonly ILog _logger;
    private volatile bool _isRunning;
    private Task? _listenerTask;
    private readonly ConcurrentDictionary<string, NotificationClient> _subscribers = new();

    public CacheServer(int port, ICacheManager cacheManager)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(cacheManager);

        _listener = new TcpListener(IPAddress.Any, port);
        _cacheManager = cacheManager;
        _logger = LogManager.GetLogger(typeof(CacheServer));

        _cacheManager.EventNotifier.CacheEventOccurred += OnCacheEvent;
    }

    public void Start()
    {
        _isRunning = true;
        _listener.Start();
        _logger.Info(CacheServerConstants.CacheServerStarted);

        _listenerTask = Task.Run(ListenForClientsAsync);
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

        _logger.Info(CacheServerConstants.CacheServerStopped);
    }

    private async Task ListenForClientsAsync()
    {
        while (_isRunning)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch (Exception ex)
            {
                if (_isRunning)
                    _logger.Error(CacheServerConstants.ErrorAcceptingClient, ex);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        var shouldClose = true;
        try
        {
            await using var stream = client.GetStream();
            var buffer = new byte[4096];    
            int bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false);

            string requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var request = JsonConvert.DeserializeObject<CacheRequest>(requestJson);

            // Check for notification subscription
            if (request?.Operation?.ToUpperInvariant() == "SUBSCRIBE")
            {
                _logger.Info("Received subscription request. handling locally.");
                RegisterNotificationClient(client, request);
                shouldClose = false; // We take ownership
                return;
            }

            CacheResponse response = ProcessRequest(request);

            string responseJson = JsonConvert.SerializeObject(response);
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);

            await stream.WriteAsync(responseBytes).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(CacheServerConstants.ErrorHandlingClient, ex);
        }
        finally
        {
            if (shouldClose)
            {
                client.Close();
            }
        }
    }

    internal CacheResponse ProcessRequest(CacheRequest? request)
    {
        if (request is null)
        {
            return new CacheResponse { Success = false, Error = "Invalid request" };
        }

        try
        {
            return request.Operation?.ToUpperInvariant() switch
            {
                CacheServerConstants.CREATE => new CacheResponse { Success = _cacheManager.Create(request.Key!, request.Value, request.ExpirationSeconds) },
                CacheServerConstants.READ => new CacheResponse { Success = true, Value = _cacheManager.Read(request.Key!) },
                CacheServerConstants.UPDATE => new CacheResponse { Success = _cacheManager.Update(request.Key!, request.Value, request.ExpirationSeconds) },
                CacheServerConstants.DELETE => new CacheResponse { Success = _cacheManager.Delete(request.Key!) },
                _ => new CacheResponse { Success = false, Error = CacheServerConstants.InvalidOperation }
            };
        }
        catch (Exception ex)
        {
            _logger.Error(CacheServerConstants.ProcessingRequestFailed, ex);
            return new CacheResponse { Success = false, Error = ex.Message };
        }
    }

    private void RegisterNotificationClient(TcpClient tcpClient, CacheRequest initialRequest)
    {
        if (!_isRunning)
        {
             tcpClient.Close();
             return;
        }

        var clientId = Guid.NewGuid().ToString();
        var notificationClient = new NotificationClient(clientId, tcpClient);

        // Handle initial subscription immediately
        if (initialRequest?.Operation?.ToUpperInvariant() == "SUBSCRIBE")
        {
             notificationClient.SubscribedEvents = initialRequest.SubscribedEventTypes?
                .Select(e => Enum.Parse<CacheEventType>(e))
                .ToHashSet() ?? Enum.GetValues<CacheEventType>().ToHashSet();
             
             _logger.Info($"Client {clientId} registered and subscribed to events: {string.Join(", ", notificationClient.SubscribedEvents)}");
        }

        if (_subscribers.TryAdd(clientId, notificationClient))
        {
            _ = HandleNotificationClientAsync(notificationClient);
        }
        else
        {
             tcpClient.Close();
        }
    }

    private async Task HandleNotificationClientAsync(NotificationClient client)
    {
        try
        {
            var stream = client.TcpClient.GetStream();
            var buffer = new byte[4096];

            // Send Ack for subscription if we just registered them
            var ack = new CacheResponse { Success = true };
            await SendToClientAsync(client, ack).ConfigureAwait(false);

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

                    _logger.Info($"Client {client.Id} updated subscription: {string.Join(", ", client.SubscribedEvents)}");

                    var response = new CacheResponse { Success = true };
                    await SendToClientAsync(client, response).ConfigureAwait(false);
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
}

internal sealed class NotificationClient(string id, TcpClient tcpClient)
{
    public string Id { get; } = id;
    public TcpClient TcpClient { get; } = tcpClient;
    public HashSet<CacheEventType> SubscribedEvents { get; set; } = [];
    public object WriteLock { get; } = new();
}
