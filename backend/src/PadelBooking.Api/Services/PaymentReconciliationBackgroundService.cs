using PadelBooking.Application.Payments;

namespace PadelBooking.Api.Services;

public sealed class PaymentReconciliationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentReconciliationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ReconcileExpiredCheckouts>()
                    .ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("Provera isteklih Stripe Checkout sesija nije uspela ({ErrorType}).", exception.GetType().Name);
            }

            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
