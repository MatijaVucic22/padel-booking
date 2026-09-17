using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Payments;

public sealed record PaymentStatusResult(int ReservationId, string Status, bool Confirmed, string Purpose);

public sealed class GetPaymentStatus(
    IPaymentRepository payments,
    IPaymentGateway gateway,
    ProcessCheckoutEvent processCheckoutEvent)
{
    public async Task<PaymentStatusResult?> ExecuteAsync(
        string sessionId, int userId, CancellationToken cancellationToken = default)
    {
        var payment = await payments.GetBySessionIdAsync(sessionId, cancellationToken);
        if (payment is null || payment.Reservation.UserId != userId) return null;

        if (payment.Status == PaymentStatus.Pending)
        {
            // The redirect is not proof of payment; ask Stripe for the server-side state.
            var session = await gateway.GetSessionAsync(sessionId, cancellationToken);
            if (session.SessionId != payment.ExternalSessionId ||
                !string.Equals(session.CustomerEmail, payment.Reservation.User.Email, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(session.ClientReferenceId) ||
                session.ClientReferenceId != session.MetadataReference)
                throw new InvalidOperationException("Stripe sesija ne odgovara rezervaciji.");

            if (session.Type is "checkout.session.completed" or "checkout.session.expired" or
                "checkout.session.async_payment_failed")
            {
                await processCheckoutEvent.ExecuteAsync(session, cancellationToken);
                payment = await payments.GetBySessionIdAsync(sessionId, cancellationToken);
                if (payment is null) return null;
            }
        }

        return new PaymentStatusResult(payment.ReservationId, payment.Status.ToString(),
            payment.Status == PaymentStatus.Paid && payment.Reservation.Status == "Active",
            payment.Purpose.ToString());
    }
}
