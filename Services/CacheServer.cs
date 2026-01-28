using log4net;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Manager;
using CacheServerModels;

namespace CacheServer.Server;

public sealed class CacheServer
{
    private readonly TcpListener _listener;
    private readonly ICacheManager _cacheManager;
    private readonly ILog _logger;
    private volatile bool _isRunning;
    private Task? _listenerTask;

    public CacheServer(int port, ICacheManager cacheManager)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        ArgumentNullException.ThrowIfNull(cacheManager);

        _listener = new TcpListener(IPAddress.Any, port);
        _cacheManager = cacheManager;
        _logger = LogManager.GetLogger(typeof(CacheServer));
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
        try
        {
            await using var stream = client.GetStream();
            var buffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false);

            string requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var request = JsonConvert.DeserializeObject<CacheRequest>(requestJson);

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
            client.Close();
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
}
