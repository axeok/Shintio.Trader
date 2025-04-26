using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shintio.Trader.Services;

public class AppService : BackgroundService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<AppService> _logger;

    public AppService(
        IHostApplicationLifetime lifetime,
        ILogger<AppService> logger
    )
    {
        _lifetime = lifetime;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting {Name}...", GetType().Name);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Executing {Name}...", GetType().Name);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping {Name}...", GetType().Name);

        return Task.CompletedTask;
    }
}