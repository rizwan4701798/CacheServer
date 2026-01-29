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
    private NotificationServer? _notificationServer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            int port = _configuration.GetValue<int>(
                CacheServerConstants.CacheSettingsPortConfigName,
                5050);

            int notificationPort = _configuration.GetValue<int>(
                CacheServerConstants.CacheSettingsNotificationPortConfigName,
                5051);

            _logger.Info($"Starting Cache Server on port {port}");

            _cacheServer = new CacheServer.Server.CacheServer(
                port,
                _cacheManager);

            _cacheServer.Start();

            _logger.Info("Cache Server started successfully");

            _logger.Info($"Starting Notification Server on port {notificationPort}");

            _notificationServer = new NotificationServer(
                notificationPort,
                _cacheManager);

            _notificationServer.Start();

            _logger.Info("Notification Server started successfully");
        }
        catch (Exception ex)
        {
            _logger.Fatal("Server failed to start", ex);
            throw; 
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.Info("Stopping servers");

            _notificationServer?.Stop();
            _logger.Info("Notification Server stopped");

            _cacheServer?.Stop();
            _logger.Info("Cache Server stopped");

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
