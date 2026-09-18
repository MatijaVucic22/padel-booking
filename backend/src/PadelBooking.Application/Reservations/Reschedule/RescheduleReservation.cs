using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Notifications;
using PadelBooking.Application.Payments;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Reservations.Reschedule;

public sealed class RescheduleReservation(
    IReservationRepository reservations,
    IPaymentRepository payments,
    ICourtRepository courts,
    IBlockedPeriodRepository blockedPeriods,
    IUnitOfWork unitOfWork,
    ICourtAdvisoryLockService courtLock,
    IBookingTimeService bookingTime,
    IPaymentGateway gateway,
    IEmailService email,
    ICourtChangeNotifier notifier,
    IReservationNotificationLogger logger)
{
    public async Task<RescheduleReservationResult> ExecuteAsync(
        RescheduleReservationCommand command,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        var courtId = await reservations.GetCourtIdForUserAsync(
            command.Id, command.UserId, cancellationToken);
        if (courtId is null) return new(RescheduleReservationStatus.NotFound);

        var acquiredLock = await courtLock.TryAcquireAsync(courtId.Value, cancellationToken);
        if (acquiredLock is null) return new(RescheduleReservationStatus.LockTimeout);

        Reservation? reservation = null;
        ReservationRescheduledEmail? emailMessage = null;
        RescheduleQuote? quote = null;
        string? checkoutUrl = null;
        DateTime oldStartTime = default;

        await using (acquiredLock)
        {
            reservation = await reservations.GetByIdForUserAsync(
                command.Id, command.UserId, cancellationToken);
            if (reservation is null) return new(RescheduleReservationStatus.NotFound);
            if (reservation.Status != "Active" || reservation.StartTime <= bookingTime.Now)
                return new(RescheduleReservationStatus.NotActiveFuture);

            var court = await courts.GetActiveByIdAsync(reservation.CourtId, cancellationToken);
            if (court is null) return new(RescheduleReservationStatus.CourtNotFound);
            if (reservation.StartTime == command.StartTime && reservation.EndTime == command.EndTime)
                return new(RescheduleReservationStatus.SameSlot);
            if (await payments.HasPendingTopUpAsync(reservation.Id, cancellationToken))
                return new(RescheduleReservationStatus.PendingTopUp);
            if (await reservations.HasOverlapAsync(reservation.CourtId, command.StartTime,
                    command.EndTime, reservation.Id, cancellationToken) ||
                await payments.HasPendingTargetOverlapAsync(reservation.CourtId,
                    command.StartTime, command.EndTime, cancellationToken: cancellationToken))
                return new(RescheduleReservationStatus.Occupied);
            if (await blockedPeriods.HasOverlapAsync(reservation.CourtId,
                    command.StartTime, command.EndTime, cancellationToken))
                return new(RescheduleReservationStatus.Blocked);

            var newPrice = Math.Round(court.PricePerHour *
                (decimal)(command.EndTime - command.StartTime).TotalHours, 2);
            var paidCredit = await payments.GetPaidCreditAsync(reservation.Id, cancellationToken);
            var topUp = Math.Max(0m, newPrice - paidCredit);
            quote = new(reservation.TotalPrice, newPrice, paidCredit, topUp,
                Math.Max(0m, paidCredit - newPrice), newPrice < reservation.TotalPrice);
            if (previewOnly) return new(RescheduleReservationStatus.Success, Quote: quote);
            if (command.ExpectedNewPrice != newPrice || command.ExpectedTopUpAmount != topUp)
                return new(RescheduleReservationStatus.QuoteChanged, Quote: quote);
            if (quote.RequiresNoRefundConfirmation && !command.AcknowledgeNoRefund)
                return new(RescheduleReservationStatus.ConfirmationRequired, Quote: quote);

            if (topUp > 0m)
            {
                var expiresAtUtc = CheckoutExpirationPolicy.GetExpirationUtc(command.StartTime, bookingTime);
                if (expiresAtUtc is null)
                    return new(RescheduleReservationStatus.CheckoutWindowClosed);

                CheckoutSession session;
                try
                {
                    session = await gateway.CreateCheckoutAsync(new CheckoutRequest(
                        Guid.NewGuid().ToString("N"), court.Name, reservation.User.Email,
                        checked((long)(topUp * 100m)), "RSD", expiresAtUtc.Value), cancellationToken);
                }
                catch (CheckoutWindowClosedException)
                {
                    return new(RescheduleReservationStatus.CheckoutWindowClosed);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch { return new(RescheduleReservationStatus.ProviderUnavailable); }

                if (string.IsNullOrWhiteSpace(session.Url))
                    return new(RescheduleReservationStatus.ProviderUnavailable);

                payments.Add(new Payment
                {
                    ReservationId = reservation.Id,
                    Amount = topUp,
                    Currency = "RSD",
                    Purpose = PaymentPurpose.RescheduleTopUp,
                    Status = PaymentStatus.Pending,
                    Provider = "Stripe",
                    ExternalSessionId = session.Id,
                    CreatedAtUtc = bookingTime.UtcNow,
                    SessionExpiresAtUtc = session.ExpiresAtUtc,
                    TargetStartTime = command.StartTime,
                    TargetEndTime = command.EndTime,
                    TargetTotalPrice = newPrice
                });
                try { await unitOfWork.SaveChangesAsync(cancellationToken); }
                catch
                {
                    try { await gateway.ExpireSessionAsync(session.Id, CancellationToken.None); }
                    catch { /* Preserve the persistence error. */ }
                    throw;
                }
                checkoutUrl = session.Url;
            }
            else
            {
                oldStartTime = reservation.StartTime;
                var oldEndTime = reservation.EndTime;
                reservation.StartTime = command.StartTime;
                reservation.EndTime = command.EndTime;
                reservation.TotalPrice = newPrice;
                reservation.ReminderSentAtUtc = null;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                emailMessage = new ReservationRescheduledEmail(
                    reservation.User.Email, reservation.User.FirstName, court.Name,
                    oldStartTime, oldEndTime, reservation.StartTime,
                    reservation.EndTime, reservation.TotalPrice, reservation.Id);
            }
        }

        if (checkoutUrl is not null)
        {
            try { await notifier.NotifyAvailabilityChangedAsync(courtId.Value, command.StartTime, cancellationToken); }
            catch { /* The persisted hold remains authoritative. */ }
            return new(RescheduleReservationStatus.CheckoutRequired, Quote: quote, CheckoutUrl: checkoutUrl);
        }

        try
        {
            await notifier.NotifyAvailabilityChangedAsync(courtId.Value, oldStartTime, cancellationToken);
            if (oldStartTime.Date != command.StartTime.Date)
                await notifier.NotifyAvailabilityChangedAsync(courtId.Value, command.StartTime, cancellationToken);
        }
        catch { /* The persisted reservation remains authoritative. */ }

        try { await email.SendReservationRescheduledAsync(emailMessage!, cancellationToken); }
        catch (Exception exception)
        {
            logger.LogEmailFailure(exception, reservation!.Id, "reschedule confirmation", warning: true);
        }

        return new(RescheduleReservationStatus.Success,
            new RescheduledReservation(reservation!.Id, reservation.CourtId,
                reservation.StartTime, reservation.EndTime,
                reservation.TotalPrice, reservation.Status), quote);
    }
}
