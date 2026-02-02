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
            return request switch
            {
                DataRequest dr when dr.Operation == CacheOperation.Create => new CacheResponse { Success = _cacheManager.Create(dr.Key, dr.Value, dr.ExpirationSeconds) },
                KeyRequest kr when kr.Operation == CacheOperation.Read => new CacheResponse { Success = true, Value = _cacheManager.Read(kr.Key) },
                DataRequest dr when dr.Operation == CacheOperation.Update => new CacheResponse { Success = _cacheManager.Update(dr.Key, dr.Value, dr.ExpirationSeconds) },
                KeyRequest kr when kr.Operation == CacheOperation.Delete => new CacheResponse { Success = _cacheManager.Delete(kr.Key) },
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
