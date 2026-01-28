using Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using log4net;

public class CacheHostedService : IHostedService
{
    private readonly ILog _logger;
       

    private readonly IConfiguration _configuration;
    private readonly ICacheManager _cacheManager;
    private CacheServer.Server.CacheServer? _cacheServer;

    public CacheHostedService(
        IConfiguration configuration,
        ICacheManager cacheManager)
    {
        _configuration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));

        _logger = LogManager.GetLogger(typeof(CacheHostedService));

        _cacheManager = cacheManager
            ?? throw new ArgumentNullException(nameof(cacheManager));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            int port = _configuration.GetValue<int>(
                CacheServerConstants.CacheSettingsPortConfigName,
                5050);

            _logger.Info($"Starting Cache Server on port {port}");

            _cacheServer = new CacheServer.Server.CacheServer(
                port,
                _cacheManager);

            _cacheServer.Start();

            _logger.Info("Cache Server started successfully");
        }
        catch (Exception ex)
        {
            _logger.Fatal("Cache Server failed to start", ex);
            throw; // Important: let host know startup failed
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.Info("Stopping Cache Server");

            _cacheServer?.Stop();

            _logger.Info("Cache Server stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.Error("Error while stopping Cache Server", ex);
        }

        return Task.CompletedTask;
    }
}
