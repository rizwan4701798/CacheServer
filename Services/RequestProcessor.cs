using CacheServerModels;
using log4net;
using Manager;

namespace CacheServer.Services;

public interface IRequestProcessor
{
    CacheResponse Process(CacheRequest request);
}

public class RequestProcessor : IRequestProcessor
{
    private readonly ICacheManager _cacheManager;
    private readonly ILog _logger;

    public RequestProcessor(ICacheManager cacheManager)
    {
        _cacheManager = cacheManager;
        _logger = LogManager.GetLogger(typeof(RequestProcessor));
    }

    public CacheResponse Process(CacheRequest request)
    {
        if (request is null)
        {
            return new CacheResponse { Success = false, Error = "Invalid request" };
        }

        try
        {
            return request.Operation switch
            {
                CacheOperation.Create => new CacheResponse { Success = _cacheManager.Create(request.Key!, request.Value, request.ExpirationSeconds) },
                CacheOperation.Read => new CacheResponse { Success = true, Value = _cacheManager.Read(request.Key!) },
                CacheOperation.Update => new CacheResponse { Success = _cacheManager.Update(request.Key!, request.Value, request.ExpirationSeconds) },
                CacheOperation.Delete => new CacheResponse { Success = _cacheManager.Delete(request.Key!) },
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
