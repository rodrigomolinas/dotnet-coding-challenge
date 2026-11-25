using dotnet.challenge.api.Services;

namespace dotnet.challenge.api.Background;

public class ReservationExpirySweeper(
    IInventoryService inventory, ILogger<ReservationExpirySweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await inventory.SweepExpiredAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Expiry sweep failed");
            }
        }
    }
}
