using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Reservations.Availability;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Payments;

public enum StartCheckoutStatus { Success, Unauthorized, CourtNotFound, Occupied, Blocked, CheckoutWindowClosed, LockTimeout, ProviderUnavailable }
public sealed record StartCheckoutResult(StartCheckoutStatus Status, int? ReservationId = null, string? Url = null);

public sealed class StartCheckout(
    IUserRepository users,
    ICourtRepository courts,
    IReservationRepository reservations,
    IBlockedPeriodRepository blockedPeriods,
    IPaymentRepository payments,
    IUnitOfWork unitOfWork,
    ICourtAdvisoryLockService courtLock,
    IBookingTimeService bookingTime,
    IPaymentGateway gateway,
    ICourtChangeNotifier notifier)
{
    public async Task<StartCheckoutResult> ExecuteAsync(
        int userId, int courtId, DateTime startTime, DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return new(StartCheckoutStatus.Unauthorized);
        if (!BookingCutoffPolicy.CanBook(startTime, bookingTime.Now))
            return new(StartCheckoutStatus.CheckoutWindowClosed);

        var acquiredLock = await courtLock.TryAcquireAsync(courtId, cancellationToken);
        if (acquiredLock is null) return new(StartCheckoutStatus.LockTimeout);

        Reservation reservation;
        CheckoutSession session;
        await using (acquiredLock)
        {
            var court = await courts.GetActiveByIdAsync(courtId, cancellationToken);
            if (court is null) return new(StartCheckoutStatus.CourtNotFound);
            if (await reservations.HasOverlapAsync(courtId, startTime, endTime, null, cancellationToken))
                return new(StartCheckoutStatus.Occupied);
            if (await payments.HasPendingTargetOverlapAsync(courtId, startTime, endTime,
                    cancellationToken: cancellationToken))
                return new(StartCheckoutStatus.Occupied);
            if (await blockedPeriods.HasOverlapAsync(courtId, startTime, endTime, cancellationToken))
                return new(StartCheckoutStatus.Blocked);

            var durationHours = (decimal)(endTime - startTime).TotalHours;
            var amount = Math.Round(court.PricePerHour * durationHours, 2);
            var amountMinor = checked((long)(amount * 100m));
            var expiresAtUtc = CheckoutExpirationPolicy.GetExpirationUtc(bookingTime);
            reservation = new Reservation
            {
                UserId = userId,
                CourtId = courtId,
                StartTime = startTime,
                EndTime = endTime,
                TotalPrice = amount,
                Status = "PendingPayment",
                CreatedAt = bookingTime.UtcNow
            };
            try
            {
                session = await gateway.CreateCheckoutAsync(
                    new CheckoutRequest(Guid.NewGuid().ToString("N"), court.Name, user.Email, amountMinor, "RSD",
                        expiresAtUtc),
                    cancellationToken);
            }
            catch (CheckoutWindowClosedException)
            {
                return new(StartCheckoutStatus.CheckoutWindowClosed);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return new(StartCheckoutStatus.ProviderUnavailable);
            }

            if (string.IsNullOrWhiteSpace(session.Url)) return new(StartCheckoutStatus.ProviderUnavailable);

            reservations.Add(reservation);
            payments.Add(new Payment
            {
                Reservation = reservation,
                Amount = amount,
                Currency = "RSD",
                Status = PaymentStatus.Pending,
                Purpose = PaymentPurpose.InitialBooking,
                Provider = "Stripe",
                ExternalSessionId = session.Id,
                CreatedAtUtc = bookingTime.UtcNow,
                SessionExpiresAtUtc = session.ExpiresAtUtc
            });
            try { await unitOfWork.SaveChangesAsync(cancellationToken); }
            catch
            {
                // A Checkout session without a reservation must not be payable.
                try { await gateway.ExpireSessionAsync(session.Id, CancellationToken.None); }
                catch { /* Preserve the original persistence failure. */ }
                throw;
            }
        }

        // SignalR only hints clients to read availability from the database.
        try { await notifier.NotifyAvailabilityChangedAsync(courtId, startTime, cancellationToken); }
        catch { /* Checkout has already been persisted. */ }
        return new(StartCheckoutStatus.Success, reservation.Id, session.Url);
    }
}
