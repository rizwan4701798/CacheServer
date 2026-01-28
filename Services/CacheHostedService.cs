using Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using log4net;
using CacheServer.Server;

public class CacheHostedService : IHostedService
{
    private readonly ILog _logger;

    private readonly IConfiguration _configuration;
    private readonly ICacheManager _cacheManager;
    private CacheServer.Server.CacheServer? _cacheServer;
    private NotificationServer? _notificationServer;

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

            int notificationPort = _configuration.GetValue<int>(
                CacheServerConstants.CacheSettingsNotificationPortConfigName,
                5051);

            // Start Cache Server
            _logger.Info($"Starting Cache Server on port {port}");

            _cacheServer = new CacheServer.Server.CacheServer(
                port,
                _cacheManager);

            _cacheServer.Start();

            _logger.Info("Cache Server started successfully");

            // Start Notification Server
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
            throw; // Important: let host know startup failed
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.Info("Stopping servers");

            // Stop Notification Server first
            _notificationServer?.Stop();
            _logger.Info("Notification Server stopped");

            // Then stop Cache Server
            _cacheServer?.Stop();
            _logger.Info("Cache Server stopped");

            _logger.Info("All servers stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.Error("Error while stopping servers", ex);
        }

        return Task.CompletedTask;
    }
}
