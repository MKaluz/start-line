using StartLine.Application.Registrations;

namespace StartLine.Worker;

public class ReservationExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpiryWorker> _logger;
    private readonly TimeSpan _interval;

    public ReservationExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ReservationExpiryWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = configuration.GetValue<int>("Worker:ExpiryIntervalSeconds", 60);
        _interval = TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReservationExpiryWorker started. Sweep interval: {Interval}.", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSweepAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task RunSweepAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
            var count = await repository.ExpireReservationsAsync(ct);
            if (count > 0)
                _logger.LogInformation("ReservationExpiryWorker expired {Count} reservation(s).", count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down — swallow cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReservationExpiryWorker sweep failed.");
        }
    }
}
