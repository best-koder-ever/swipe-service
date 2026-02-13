using Microsoft.Extensions.Options;
using SwipeService.Models;

namespace SwipeService.Services;

/// <summary>
/// T190: Background hosted service that periodically recalculates trust scores
/// for all users with recent activity. Runs every BackgroundRecalcIntervalHours.
/// </summary>
public class SwipeBehaviorRecalcService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SwipeBehaviorConfiguration> _config;
    private readonly ILogger<SwipeBehaviorRecalcService> _logger;

    public SwipeBehaviorRecalcService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SwipeBehaviorConfiguration> config,
        ILogger<SwipeBehaviorRecalcService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SwipeBehaviorRecalcService started");

        // Wait 2 minutes after startup before first run
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var analyzer = scope.ServiceProvider.GetRequiredService<ISwipeBehaviorAnalyzer>();

                var recalculated = await analyzer.RecalculateAllAsync(stoppingToken);
                _logger.LogInformation("Background recalc completed: {Count} users", recalculated);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background trust score recalculation");
            }

            var interval = TimeSpan.FromHours(_config.CurrentValue.BackgroundRecalcIntervalHours);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("SwipeBehaviorRecalcService stopped");
    }
}
