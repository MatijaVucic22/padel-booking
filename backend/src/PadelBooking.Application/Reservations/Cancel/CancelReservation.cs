using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Notifications;

namespace PadelBooking.Application.Reservations.Cancel;

public sealed class CancelReservation
{
    private readonly IReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentRepository _payments;
    private readonly ICourtAdvisoryLockService _courtLock;
    private readonly IBookingTimeService _bookingTime;
    private readonly IEmailService _email;
    private readonly ICourtChangeNotifier _notifier;
    private readonly IReservationNotificationLogger _logger;

    public CancelReservation(IReservationRepository reservations,
        IUnitOfWork unitOfWork, IPaymentRepository payments,
        ICourtAdvisoryLockService courtLock, IBookingTimeService bookingTime,
        IEmailService email, ICourtChangeNotifier notifier,
        IReservationNotificationLogger logger)
    {
        _reservations = reservations; _unitOfWork = unitOfWork;
        _payments = payments; _courtLock = courtLock;
        _bookingTime = bookingTime; _email = email;
        _notifier = notifier; _logger = logger;
    }

    public async Task<CancelReservationResult> ExecuteAsync(
        int id, int userId, bool acknowledgeNoRefund,
        CancellationToken cancellationToken = default)
    {
        var courtId = await _reservations.GetCourtIdForUserAsync(id, userId, cancellationToken);
        if (courtId is null) return new(CancelReservationStatus.NotFound);
        var acquiredLock = await _courtLock.TryAcquireAsync(courtId.Value, cancellationToken);
        if (acquiredLock is null) return new(CancelReservationStatus.LockTimeout);

        PadelBooking.Domain.Entities.Reservation reservation;
        await using (acquiredLock)
        {
            var current = await _reservations.GetByIdForUserAsync(id, userId, cancellationToken);
            if (current is null) return new(CancelReservationStatus.NotFound);
            reservation = current;
            if (reservation.Status == "Cancelled") return new(CancelReservationStatus.AlreadyCancelled);
            if (reservation.Status != "Active") return new(CancelReservationStatus.NotActive);
            if (await _payments.HasPendingTopUpAsync(id, cancellationToken))
                return new(CancelReservationStatus.PendingTopUp);

            var now = _bookingTime.Now;
            if (reservation.EndTime <= now) return new(CancelReservationStatus.Completed);
            if (reservation.StartTime <= now) return new(CancelReservationStatus.Started);

            var paidAmounts = await _payments.GetAppliedPaidAmountsAsync(
                [reservation.Id], cancellationToken);
            if (paidAmounts.GetValueOrDefault(reservation.Id) > 0 && !acknowledgeNoRefund)
                return new(CancelReservationStatus.NoRefundAcknowledgementRequired);

            reservation.Status = "Cancelled";
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        await _notifier.NotifyAvailabilityChangedAsync(
            reservation.CourtId, reservation.StartTime, cancellationToken);

        try
        {
            await _email.SendReservationCancellationAsync(
                new ReservationCancellationEmail(reservation.User.Email,
                    reservation.User.FirstName, reservation.Court.Name,
                    reservation.Court.Location, reservation.StartTime,
                    reservation.EndTime, reservation.TotalPrice, reservation.Id),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogEmailFailure(exception, reservation.Id, "cancellation");
        }

        return new(CancelReservationStatus.Success);
    }
}
