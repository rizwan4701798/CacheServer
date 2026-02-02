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
            return new ErrorResponse(CacheServerConstants.InvalidRequest);
        }

        try
        {
            return request switch
            {
                DataRequest dr when dr.Operation == CacheOperation.Create => 
                    _cacheManager.Create(dr.Key, dr.Value, dr.ExpirationSeconds) ? new SuccessResponse() : new CacheResponse { Success = false },
                KeyRequest kr when kr.Operation == CacheOperation.Read => 
                    new DataResponse(_cacheManager.Read(kr.Key)),
                DataRequest dr when dr.Operation == CacheOperation.Update => 
                    _cacheManager.Update(dr.Key, dr.Value, dr.ExpirationSeconds) ? new SuccessResponse() : new CacheResponse { Success = false },
                KeyRequest kr when kr.Operation == CacheOperation.Delete => 
                    _cacheManager.Delete(kr.Key) ? new SuccessResponse() : new CacheResponse { Success = false },
                _ => new ErrorResponse(CacheServerConstants.InvalidOperation)
            };
        }
        catch (Exception ex)
        {
            _logger.Error(CacheServerConstants.ProcessingRequestFailed, ex);
            return new ErrorResponse(ex.Message);
        }
    }
}
