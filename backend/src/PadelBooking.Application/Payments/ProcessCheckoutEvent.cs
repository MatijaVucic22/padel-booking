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
    IReservationRepository reservations,
    IBlockedPeriodRepository blockedPeriods,
    IUnitOfWork unitOfWork,
    ICourtAdvisoryLockService courtLock,
    IBookingTimeService bookingTime,
    IEmailService email,
    IReservationNotificationLogger notificationLogger,
    IPaymentResolutionLogger resolutionLogger,
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
        ReservationRescheduledEmail? rescheduleConfirmation = null;
        (int PaymentId, int ReservationId, PaymentPurpose Purpose, string ReasonCode)? resolution = null;
        int courtId;
        DateTime oldStartTime;
        DateTime oldEndTime;
        DateTime targetStartTime;
        await using (acquiredLock)
        {
            var payment = await payments.GetTrackedBySessionIdAsync(checkoutEvent.SessionId, cancellationToken);
            if (payment is null || payment.Status != PaymentStatus.Pending) return;

            var reservation = payment.Reservation;
            courtId = reservation.CourtId;
            oldStartTime = reservation.StartTime;
            oldEndTime = reservation.EndTime;
            targetStartTime = payment.Purpose == PaymentPurpose.RescheduleTopUp
                ? payment.TargetStartTime ?? reservation.StartTime : reservation.StartTime;

            if (checkoutEvent.Type is "checkout.session.completed" or "checkout.session.async_payment_succeeded")
            {
                var expectedMinor = checked((long)(payment.Amount * 100m));
                if (checkoutEvent.PaymentStatus != "paid" ||
                    checkoutEvent.AmountTotalMinor != expectedMinor ||
                    !string.Equals(checkoutEvent.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
                    return;

                string? reasonCode = null;
                if (payment.Purpose == PaymentPurpose.RescheduleTopUp)
                {
                    if (reservation.Status != "Active" || reservation.StartTime <= bookingTime.Now)
                        reasonCode = "ORIGINAL_NOT_FUTURE_ACTIVE";
                    else if (!reservation.Court.IsActive)
                        reasonCode = "COURT_INACTIVE";
                    else if (payment.TargetStartTime is null || payment.TargetEndTime is null ||
                        payment.TargetTotalPrice is null)
                        reasonCode = "TARGET_MISSING";
                    else if (payment.TargetStartTime.Value <= bookingTime.Now)
                        reasonCode = "TARGET_STARTED";
                    else if (await reservations.HasOverlapAsync(courtId, payment.TargetStartTime.Value,
                        payment.TargetEndTime.Value, reservation.Id, cancellationToken) ||
                        await payments.HasPendingTargetOverlapAsync(courtId, payment.TargetStartTime.Value,
                            payment.TargetEndTime.Value, payment.Id, cancellationToken))
                        reasonCode = "TARGET_OCCUPIED";
                    else if (await blockedPeriods.HasOverlapAsync(courtId, payment.TargetStartTime.Value,
                        payment.TargetEndTime.Value, cancellationToken))
                        reasonCode = "TARGET_BLOCKED";

                    if (reasonCode is null)
                    {
                        reservation.StartTime = payment.TargetStartTime!.Value;
                        reservation.EndTime = payment.TargetEndTime!.Value;
                        reservation.TotalPrice = payment.TargetTotalPrice!.Value;
                        reservation.ReminderSentAtUtc = null;
                        rescheduleConfirmation = new ReservationRescheduledEmail(
                            reservation.User.Email, reservation.User.FirstName,
                            reservation.Court.Name, oldStartTime, oldEndTime,
                            reservation.StartTime, reservation.EndTime,
                            reservation.TotalPrice, reservation.Id);
                    }
                }
                else
                {
                    if (reservation.Status != "PendingPayment")
                        reasonCode = "RESERVATION_NOT_PENDING";
                    else if (reservation.StartTime <= bookingTime.Now)
                        reasonCode = "BOOKING_STARTED";
                    else if (!reservation.Court.IsActive)
                        reasonCode = "COURT_INACTIVE";
                    else if (await reservations.HasOverlapAsync(courtId, reservation.StartTime,
                        reservation.EndTime, reservation.Id, cancellationToken))
                        reasonCode = "BOOKING_OCCUPIED";
                    else if (await blockedPeriods.HasOverlapAsync(courtId, reservation.StartTime,
                        reservation.EndTime, cancellationToken))
                        reasonCode = "BOOKING_BLOCKED";

                    if (reasonCode is null)
                    {
                        reservation.Status = "Active";
                        confirmation = new ReservationConfirmationEmail(
                            reservation.User.Email, reservation.User.FirstName,
                            reservation.Court.Name, reservation.Court.Location,
                            reservation.StartTime, reservation.EndTime,
                            reservation.TotalPrice, reservation.Id);
                    }
                    else if (reservation.Status == "PendingPayment")
                    {
                        // A paid, unfulfilled booking must not retain its provisional slot.
                        reservation.Status = "Cancelled";
                    }
                }

                payment.Status = PaymentStatus.Paid;
                payment.FulfillmentStatus = reasonCode is null
                    ? PaymentFulfillmentStatus.Applied : PaymentFulfillmentStatus.RequiresResolution;
                payment.ResolutionReasonCode = reasonCode;
                payment.ExternalSessionId = checkoutEvent.SessionId;
                payment.ExternalPaymentIntentId = checkoutEvent.PaymentIntentId;
                if (reasonCode is not null)
                    resolution = (payment.Id, reservation.Id, payment.Purpose, reasonCode);
            }
            else
            {
                payment.Status = checkoutEvent.Type == "checkout.session.expired"
                    ? PaymentStatus.Cancelled : PaymentStatus.Failed;
                payment.FulfillmentStatus = PaymentFulfillmentStatus.NotApplicable;
                if (payment.Purpose == PaymentPurpose.InitialBooking)
                    reservation.Status = "Cancelled";
            }

            payment.UpdatedAtUtc = bookingTime.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (resolution is { } unresolved)
        {
            try
            {
                resolutionLogger.LogRequiresResolution(unresolved.PaymentId, unresolved.ReservationId,
                    unresolved.Purpose, unresolved.ReasonCode);
            }
            catch { /* A logging failure cannot undo a persisted provider payment. */ }
        }

        try
        {
            await notifier.NotifyAvailabilityChangedAsync(courtId, targetStartTime, cancellationToken);
            if (rescheduleConfirmation is not null && oldStartTime.Date != targetStartTime.Date)
                await notifier.NotifyAvailabilityChangedAsync(courtId, oldStartTime, cancellationToken);
        }
        catch { /* Persisted payment remains the source of truth. */ }

        if (confirmation is not null)
        {
            try { await email.SendReservationConfirmationAsync(confirmation, CancellationToken.None); }
            catch (Exception exception)
            {
                notificationLogger.LogEmailFailure(exception, confirmation.ReservationId, "payment confirmation");
            }
        }
        if (rescheduleConfirmation is not null)
        {
            try { await email.SendReservationRescheduledAsync(rescheduleConfirmation, CancellationToken.None); }
            catch (Exception exception)
            {
                notificationLogger.LogEmailFailure(exception, rescheduleConfirmation.ReservationId,
                    "reschedule confirmation", warning: true);
            }
        }
    }
}
