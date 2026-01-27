using Manager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public class CacheHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ICacheManager _cacheManager;
    private CacheServer.Server.CacheServer _cacheServer;

    public CacheHostedService(
        IConfiguration configuration,
        ICacheManager cacheManager)
    {
        _configuration = configuration;
        _cacheManager = cacheManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        int port = _configuration.GetValue<int>(CacheServerConstants.CacheSettingsPortConfigName, 5050);

        _cacheServer = new CacheServer.Server.CacheServer(port, _cacheManager);
        _cacheServer.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cacheServer?.Stop();
        return Task.CompletedTask;
    }
}
