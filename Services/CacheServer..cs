using log4net;
using Newtonsoft.Json;
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
    private bool _isRunning;

    public CacheServer(int port, ICacheManager cacheManager)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _cacheManager = cacheManager;
        _logger = LogManager.GetLogger(typeof(CacheServer));
    }

    public void Start()
    {
        _isRunning = true;
        _listener.Start();
        _logger.Info(CacheServerConstants.CacheServerStarted);

        Thread listenerThread = new Thread(ListenForClients);
        listenerThread.Start();
    }

    public void Stop()
    {
        _isRunning = false;
        _listener.Stop();
        _logger.Info(CacheServerConstants.CacheServerStopped);
    }

    private void ListenForClients()
    {
        while (_isRunning)
        {
            try
            {
                var client = _listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(HandleClient, client);
            }
            catch (Exception ex)
            {
                if (_isRunning)
                    _logger.Error(CacheServerConstants.ErrorAcceptingClient, ex);
            }
        }
    }

    private void HandleClient(object obj)
    {
        TcpClient client = (TcpClient)obj;

        try
        {
            using var stream = client.GetStream();
            byte[] buffer = new byte[4096];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            string requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            CacheRequest request = JsonConvert.DeserializeObject<CacheRequest>(requestJson);

            CacheResponse response = ProcessRequest(request);

            string responseJson = JsonConvert.SerializeObject(response);
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);

            stream.Write(responseBytes, 0, responseBytes.Length);
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

    internal CacheResponse ProcessRequest(CacheRequest request)
    {
        try
        {
            return request.Operation.ToUpper() switch
            {
                CacheServerConstants.CREATE => new CacheResponse { Success = _cacheManager.Create(request.Key, request.Value, request.ExpirationSeconds) },
                CacheServerConstants.READ => new CacheResponse { Success = true, Value = _cacheManager.Read(request.Key) },
                CacheServerConstants.UPDATE => new CacheResponse { Success = _cacheManager.Update(request.Key, request.Value, request.ExpirationSeconds) },
                CacheServerConstants.DELETE=> new CacheResponse { Success = _cacheManager.Delete(request.Key) },
                _ => new CacheResponse { Success = false, Error = CacheServerConstants.InvalidOperation}
            };
        }
        catch (Exception ex)
        {
            _logger.Error(CacheServerConstants.ProcessingRequestFailed, ex);
            return new CacheResponse { Success = false, Error = ex.Message };
        }
    }
}
