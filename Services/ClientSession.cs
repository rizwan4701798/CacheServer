using System.Net.Sockets;
using System.Text;
using CacheServerModels;
using log4net;
using Newtonsoft.Json;

namespace CacheServer.Services;

public interface IClientSession
{
    Task SendAsync(CacheResponse response);
}

public class ClientSession : IClientSession
{
    private readonly TcpClient _tcpClient;
    private readonly string _clientId;
    private readonly IRequestProcessor _requestProcessor;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ILog _logger;
    private readonly object _writeLock = new();

    public ClientSession(
        TcpClient tcpClient, 
        string clientId,
        IRequestProcessor requestProcessor,
        ISubscriptionManager subscriptionManager)
    {
        _tcpClient = tcpClient;
        _clientId = clientId;
        _requestProcessor = requestProcessor;
        _subscriptionManager = subscriptionManager;
        _logger = LogManager.GetLogger(typeof(ClientSession));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.Info(string.Format(CacheServerConstants.ClientConnected, _clientId));
        _subscriptionManager.AddClient(_clientId, this);

        try
        {
            using var stream = _tcpClient.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            using var jsonReader = new JsonTextReader(reader) { CloseInput = false, SupportMultipleContent = true };
            var serializer = new JsonSerializer();

            while (!cancellationToken.IsCancellationRequested && _tcpClient.Connected)
            {
                 if (!await jsonReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        break;

                 var request = serializer.Deserialize<CacheRequest>(jsonReader);
                 if (request == null) continue;

                 CacheResponse response;

                 if (request.Operation == CacheOperation.Subscribe)
                 {
                     var subRequest = request as SubscriptionRequest;
                     var events = subRequest?.SubscribedEventTypes?
                            .Select(e => Enum.Parse<CacheEventType>(e))
                            .ToHashSet() ?? Enum.GetValues<CacheEventType>().ToHashSet();
                     
                     _subscriptionManager.Subscribe(_clientId, events);
                     _logger.Info(string.Format(CacheServerConstants.ClientSubscribed, _clientId, string.Join(", ", events)));
                     response = new SuccessResponse();
                 }
                 else if (request.Operation == CacheOperation.Unsubscribe)
                 {
                     var subRequest = request as SubscriptionRequest;
                     if (subRequest != null && subRequest.SubscribedEventTypes != null && subRequest.SubscribedEventTypes.Length > 0)
                     {
                         var events = subRequest.SubscribedEventTypes
                                .Select(e => Enum.Parse<CacheEventType>(e))
                                .ToHashSet();
                         _subscriptionManager.Unsubscribe(_clientId, events);
                         _logger.Info(string.Format("Client {0} unsubscribed from: {1}", _clientId, string.Join(", ", events)));
                     }
                     else
                     {
                         _subscriptionManager.Unsubscribe(_clientId);
                         _logger.Info(string.Format(CacheServerConstants.ClientUnsubscribed, _clientId));
                     }
                     response = new SuccessResponse();
                 }
                 else
                 {
                     response = _requestProcessor.Process(request);
                 }

                 await SendAsync(response).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (JsonException)
        {
             _logger.Warn(string.Format(CacheServerConstants.ClientInvalidJson, _clientId));
        }
        catch (Exception ex)
        {
             _logger.Debug(string.Format(CacheServerConstants.ClientDisconnected, _clientId, ex.Message));
        }
        finally
        {
            _subscriptionManager.RemoveClient(_clientId);
            try { _tcpClient.Close(); } catch { }
            _logger.Info(string.Format(CacheServerConstants.ClientDisconnectedInfo, _clientId));
        }
    }

    public async Task SendAsync(CacheResponse response)
    {
        if (!_tcpClient.Connected) return;

        var json = JsonConvert.SerializeObject(response);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");

        try
        {
            lock (_writeLock)
            {
                var stream = _tcpClient.GetStream();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(string.Format(CacheServerConstants.ClientWriteFailed, _clientId, ex.Message));
            try { _tcpClient.Close(); } catch { }
        }

        await Task.CompletedTask;
    }
}
