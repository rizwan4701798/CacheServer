using log4net;
using Manager;
using System.Net;
using System.Net.Sockets;
using CacheServer.Services; // Ensure using for new services

namespace CacheServer.Server;

public class CacheServer
{
    private readonly TcpListener _listener;
    private readonly ICacheManager _cacheManager;
    private readonly ILog _logger;
    private readonly RequestProcessor _requestProcessor;
    private readonly SubscriptionManager _subscriptionManager;
    
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public CacheServer(int port, ICacheManager cacheManager)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(cacheManager);

        _listener = new TcpListener(IPAddress.Any, port);
        _cacheManager = cacheManager;
        _logger = LogManager.GetLogger(typeof(CacheServer));
        
        // Initialize components
        _requestProcessor = new RequestProcessor(cacheManager);
        _subscriptionManager = new SubscriptionManager(cacheManager);
    }

    public void Start()
    {
        if (_cts != null && !_cts.IsCancellationRequested) return;

        _cts = new CancellationTokenSource();
        _listener.Start();
        _logger.Info(CacheServerConstants.CacheServerStarted);

        _listenerTask = Task.Run(() => ListenForClientsAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener.Stop();
        
        _logger.Info(CacheServerConstants.CacheServerStopped);
    }

    private async Task ListenForClientsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                var clientId = Guid.NewGuid().ToString();
                
                var session = new ClientSession(client, clientId, _requestProcessor, _subscriptionManager);
                _ = Task.Run(() => session.RunAsync(token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                 if (!token.IsCancellationRequested)
                    _logger.Error(CacheServerConstants.ErrorAcceptingClient, ex);
            }
        }
    }
}
