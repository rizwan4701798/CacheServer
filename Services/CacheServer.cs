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
            // We must call the method. Since OnCacheEvent is an event handler (void), we usually fire and forget or block.
            // Since our SendToClientAsync is actually synchronous inside the lock with a Task wrapper, we can just Wait() or use the sync approach logic that I moved into SendToClientAsync.
            // Actually, I should just fix SendToClientAsync to be consistent. 
            // Better yet, I'll update this loop to use the method I defined.
            
            // The original code closed the client connection directly.
            // If the intent is to send a "server stopping" message, a specific CacheResponse would be needed.
            // For now, we'll assume the original intent of just closing the connection is sufficient for Stop().
            // If a notification is desired, 'notification' would need to be defined as a CacheResponse.
            // For example: var notification = new CacheResponse { Success = true, Message = "Server shutting down." };
            // Then: _ = Task.Run(() => SendToClientAsync(client, notification));
            // However, the current instruction only provides a partial snippet that would introduce a compilation error.
            // Sticking to the original behavior of closing the client directly, as 'notification' is undefined.
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
        var clientId = Guid.NewGuid().ToString();
        var clientConnection = new NotificationClient(clientId, client);
        
        // Add to subscribers immediately so they can receive notifications if/when they subscribe
        _subscribers.TryAdd(clientId, clientConnection);
        _logger.Info($"Client connected: {clientId}");

        try
        {
            var stream = client.GetStream();
            
            // Use StreamReader/JsonTextReader for dynamic message parsing
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
            using (var jsonReader = new JsonTextReader(reader) { CloseInput = false, SupportMultipleContent = true })
            {
                var serializer = new JsonSerializer();

                while (_isRunning && client.Connected)
                {
                    // Read next JSON object from the stream
                    // We need to check if we can read. JsonTextReader.Read() blocks, so providing cancellation via stream closing is standard.
                    if (!await jsonReader.ReadAsync().ConfigureAwait(false))
                        break;

                    // Deserialize the current object
                    var request = serializer.Deserialize<CacheRequest>(jsonReader);
                    
                    if (request == null) continue;

                    CacheResponse response;

                    // Handle Special Sub/Unsub commands
                    if (request.Operation?.ToUpperInvariant() == "SUBSCRIBE")
                    {
                        clientConnection.SubscribedEvents = request.SubscribedEventTypes?
                            .Select(e => Enum.Parse<CacheEventType>(e))
                            .ToHashSet() ?? Enum.GetValues<CacheEventType>().ToHashSet();

                        _logger.Info($"Client {clientId} subscribed to: {string.Join(", ", clientConnection.SubscribedEvents)}");
                        response = new CacheResponse { Success = true };
                    }
                    else if (request.Operation?.ToUpperInvariant() == "UNSUBSCRIBE")
                    {
                        clientConnection.SubscribedEvents.Clear();
                        _logger.Info($"Client {clientId} unsubscribed");
                        response = new CacheResponse { Success = true };
                    }
                    else
                    {
                        // Standard CRUD
                        response = ProcessRequest(request);
                    }

                    // Send Response safely
                    await SendToClientAsync(clientConnection, response).ConfigureAwait(false);
                }
            }
        }
        catch (JsonException)
        {
             _logger.Warn($"Client {clientId} sent invalid JSON.Disconnecting.");
        }
        catch (Exception ex)
        {
            if (_isRunning)
            {
                // Common to get IO exceptions on disconnect
                _logger.Debug($"Client {clientId} disconnected: {ex.Message}");
            }
        }
        finally
        {
            _subscribers.TryRemove(clientId, out _);
            try
            {
                client.Close();
            }
            catch { }
            _logger.Info($"Client disconnected: {clientId}");
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

            // Notify asynchronously (fire and forget)
            _ = Task.Run(() => SendToClientAsync(client, notification));
        }
    }

    private async Task SendToClientAsync(NotificationClient client, CacheResponse response)
    {
        if (!client.TcpClient.Connected) return;

        // Serialize and Write synchronously inside lock to ensure atomicity of the message on the wire
        // (Async lock would be better but simple lock is safer for avoiding interleaved writes)
        // However, we are in an async method. We should prepare bytes then lock and write?
        // NetworkStream write isn't thread safe for concurrent writes.
        
        var json = JsonConvert.SerializeObject(response);
        var bytes = Encoding.UTF8.GetBytes(json + "\n"); // Newline delimiter helper for some clients

        // We use a semaphore or lock. NotificationClient has a 'object WriteLock'.
        // We cannot await inside a lock. We should use NetworkStream.Write (sync) or SemaphoreSlim.
        // Given the previous code used `lock(client.WriteLock)`, let's stick to synchronous write for safety 
        // OR use a proper async locking mechanism.
        // For simple migration, we will use the existing WriteLock and synchronous GetStream().Write
        // This blocks the thread but ensures safety.
        
        try 
        {
            // Note: If we want true async IO with concurrency, we need SemaphoreSlim in NotificationClient.
            // But 'NotificationClient' is defined at the bottom as a simple record-like class.
            // Let's upgrade the write to be safe.
            lock (client.WriteLock)
            {
                 var stream = client.TcpClient.GetStream();
                 stream.Write(bytes, 0, bytes.Length);
                 stream.Flush();
            }
        }
        catch (Exception ex)
        {
            _logger.Debug($"Failed to write to client {client.Id}: {ex.Message}");
            client.TcpClient.Close();
        }
        
        await Task.CompletedTask; // Keep signature async compatible if needed, or refactor to void
    }
    
    // Remove unused methods

}

internal sealed class NotificationClient(string id, TcpClient tcpClient)
{
    public string Id { get; } = id;
    public TcpClient TcpClient { get; } = tcpClient;
    public HashSet<CacheEventType> SubscribedEvents { get; set; } = [];
    public object WriteLock { get; } = new();
}
