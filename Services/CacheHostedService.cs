using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Manager;
using System.Threading;
using System.Threading.Tasks;
using CacheServer.Server;

public class CacheHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ICacheManager _cacheManager;
    private CacheServer.Server.CacheServer _cacheServer;

    public CacheHostedService(
        IConfiguration configuration,
        CacheManager cacheManager)
    {
        _configuration = configuration;
        _cacheManager = cacheManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        int port = _configuration.GetValue<int>("CacheSettings:Port", 5050);

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
