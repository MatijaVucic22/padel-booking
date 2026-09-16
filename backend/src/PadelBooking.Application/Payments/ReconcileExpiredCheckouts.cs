using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Payments;

public sealed class ReconcileExpiredCheckouts(
    IPaymentRepository payments,
    IPaymentGateway gateway,
    IBookingTimeService bookingTime,
    ProcessCheckoutEvent processor)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var sessionIds = await payments.ListDueSessionIdsAsync(bookingTime.UtcNow, cancellationToken);
        var failures = new List<Exception>();
        foreach (var sessionId in sessionIds)
        {
            try
            {
                // Keep a hold if Stripe cannot be reached; do not release a possibly paid slot.
                var session = await gateway.GetSessionAsync(sessionId, cancellationToken);
                await processor.ExecuteAsync(session, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { failures.Add(exception); }
        }
        if (failures.Count > 0) throw new AggregateException("Provera pojedinih Stripe sesija nije uspela.", failures);
    }
}
