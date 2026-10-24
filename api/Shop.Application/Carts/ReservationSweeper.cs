using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shop.Application.Carts;

/// <summary>Releases expired holds every minute while the API is awake. Reads also release lazily.</summary>
public sealed class ReservationSweeper(IServiceScopeFactory scopes, ILogger<ReservationSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var released = await scope.ServiceProvider.GetRequiredService<ReservationService>()
                    .ReleaseExpiredAsync(stoppingToken);
                if (released > 0)
                    logger.LogInformation("Released {Count} expired reservations", released);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Reservation sweep failed; reads still release expired holds");
            }
        }
    }
}
