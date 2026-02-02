using Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using log4net;
using CacheServer.Server;

public class CacheHostedService(
    IConfiguration configuration,
    ICacheManager cacheManager) : IHostedService
{
    private readonly ILog _logger = LogManager.GetLogger(typeof(CacheHostedService));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ICacheManager _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));

    private CacheServer.Server.CacheServer? _cacheServer;


    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            int port = _configuration.GetValue<int>(
                CacheServerConstants.CacheSettingsPortConfigName,
                5050);

            _logger.Info(string.Format(CacheServerConstants.StartingCacheServer, port));

            _cacheServer = new CacheServer.Server.CacheServer(
                port,
                _cacheManager);

            _cacheServer.Start();

            _logger.Info(CacheServerConstants.CacheServerStartedSuccess);
        }
        catch (Exception ex)
        {
            _logger.Fatal(CacheServerConstants.ServerFailedToStart, ex);
            throw; 
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.Info(CacheServerConstants.StoppingServers);



            _cacheServer?.Stop();
            _logger.Info(CacheServerConstants.CacheServerStopped);

            if (_cacheManager is IDisposable disposableCacheManager)
            {
                disposableCacheManager.Dispose();
                _logger.Info("CacheManager disposed");
            }

            _logger.Info("All servers stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.Error("Error while stopping servers", ex);
        }

        return Task.CompletedTask;
    }
}
