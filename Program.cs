
using log4net.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Manager;

class Program
{
    static void Main(string[] args)
    {
        XmlConfigurator.Configure(
            new FileInfo(Path.Combine(AppContext.BaseDirectory, CacheServerConstants.Log4netConfigName))
        );

        Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<ICacheManager>(sp =>
                {
                    int maxItems = context.Configuration.GetValue<int>(CacheServerConstants.CacheSettingsMaxItemsConfigName, 100);
                    return new CacheManager(maxItems);
                });

                services.AddHostedService<CacheHostedService>();
            })
            .Build()
            .Run();
    }
}
