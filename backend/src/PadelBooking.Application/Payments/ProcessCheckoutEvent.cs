using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Notifications;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Payments;

public sealed class ProcessCheckoutEvent(
    IPaymentRepository payments,
    IUnitOfWork unitOfWork,
    ICourtAdvisoryLockService courtLock,
    IBookingTimeService bookingTime,
    IEmailService email,
    IReservationNotificationLogger notificationLogger,
    ICourtChangeNotifier notifier)
{
    public async Task ExecuteAsync(CheckoutEvent checkoutEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(checkoutEvent.SessionId) ||
            checkoutEvent.Type is not ("checkout.session.completed" or
                "checkout.session.async_payment_succeeded" or
                "checkout.session.expired" or
                "checkout.session.async_payment_failed")) return;

        var existing = await payments.GetBySessionIdAsync(checkoutEvent.SessionId, cancellationToken);
        if (existing is null) return;

        var acquiredLock = await courtLock.TryAcquireAsync(existing.Reservation.CourtId, cancellationToken);
        if (acquiredLock is null) throw new InvalidOperationException("Court lock nije dostupan; Stripe će ponoviti webhook.");

        ReservationConfirmationEmail? confirmation = null;
        int courtId;
        DateTime startTime;
        await using (acquiredLock)
        {
            var payment = await payments.GetTrackedByReservationIdAsync(existing.ReservationId, cancellationToken);
            if (payment is null ||
                (payment.ExternalSessionId is not null && payment.ExternalSessionId != checkoutEvent.SessionId) ||
                payment.Status != PaymentStatus.Pending) return;

            var reservation = payment.Reservation;
            courtId = reservation.CourtId;
            startTime = reservation.StartTime;

            if (checkoutEvent.Type is "checkout.session.completed" or "checkout.session.async_payment_succeeded")
            {
                var expectedMinor = checked((long)(payment.Amount * 100m));
                if (checkoutEvent.PaymentStatus != "paid" ||
                    checkoutEvent.AmountTotalMinor != expectedMinor ||
                    !string.Equals(checkoutEvent.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
                    return;

                payment.Status = PaymentStatus.Paid;
                payment.ExternalSessionId = checkoutEvent.SessionId;
                payment.ExternalPaymentIntentId = checkoutEvent.PaymentIntentId;
                reservation.Status = "Active";
                confirmation = new ReservationConfirmationEmail(
                    reservation.User.Email, reservation.User.FirstName,
                    reservation.Court.Name, reservation.Court.Location,
                    reservation.StartTime, reservation.EndTime,
                    reservation.TotalPrice, reservation.Id);
            }
            else
            {
                payment.Status = checkoutEvent.Type == "checkout.session.expired"
                    ? PaymentStatus.Cancelled : PaymentStatus.Failed;
                reservation.Status = "Cancelled";
            }

            payment.UpdatedAtUtc = bookingTime.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        try { await notifier.NotifyAvailabilityChangedAsync(courtId, startTime, cancellationToken); }
        catch { /* Persisted payment remains the source of truth. */ }

        if (confirmation is not null)
        {
            try { await email.SendReservationConfirmationAsync(confirmation, CancellationToken.None); }
            catch (Exception exception)
            {
                notificationLogger.LogEmailFailure(exception, confirmation.ReservationId, "payment confirmation");
            }
        }
    }
}
