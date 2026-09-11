using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Notifications;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Reservations.Reschedule;

public sealed class RescheduleReservation
{
    private readonly IReservationRepository _reservations;
    private readonly ICourtRepository _courts;
    private readonly IBlockedPeriodRepository _blockedPeriods;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourtAdvisoryLockService _courtLock;
    private readonly IBookingTimeService _bookingTime;
    private readonly IEmailService _email;
    private readonly ICourtChangeNotifier _notifier;
    private readonly IReservationNotificationLogger _logger;

    public RescheduleReservation(IReservationRepository reservations,
        ICourtRepository courts, IBlockedPeriodRepository blockedPeriods,
        IUnitOfWork unitOfWork, ICourtAdvisoryLockService courtLock,
        IBookingTimeService bookingTime, IEmailService email,
        ICourtChangeNotifier notifier, IReservationNotificationLogger logger)
    {
        _reservations = reservations; _courts = courts;
        _blockedPeriods = blockedPeriods; _unitOfWork = unitOfWork;
        _courtLock = courtLock; _bookingTime = bookingTime; _email = email;
        _notifier = notifier; _logger = logger;
    }

    public async Task<RescheduleReservationResult> ExecuteAsync(
        RescheduleReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        var courtId = await _reservations.GetCourtIdForUserAsync(
            command.Id, command.UserId, cancellationToken);
        if (courtId is null) return new(RescheduleReservationStatus.NotFound);

        var acquiredLock = await _courtLock.TryAcquireAsync(
            courtId.Value, cancellationToken);
        if (acquiredLock is null) return new(RescheduleReservationStatus.LockTimeout);

        DateTime oldStartTime;
        DateTime oldEndTime;
        Reservation reservation;
        ReservationRescheduledEmail emailMessage;

        await using (acquiredLock)
        {
            var lockedReservation = await _reservations.GetByIdForUserAsync(
                command.Id, command.UserId, cancellationToken);
            if (lockedReservation is null)
                return new(RescheduleReservationStatus.NotFound);
            reservation = lockedReservation;

            if (reservation.Status != "Active" ||
                reservation.StartTime <= _bookingTime.Now)
                return new(RescheduleReservationStatus.NotActiveFuture);

            var court = await _courts.GetActiveByIdAsync(
                reservation.CourtId, cancellationToken);
            if (court is null) return new(RescheduleReservationStatus.CourtNotFound);

            if (reservation.StartTime == command.StartTime &&
                reservation.EndTime == command.EndTime)
                return new(RescheduleReservationStatus.SameSlot);

            if (await _reservations.HasOverlapAsync(reservation.CourtId,
                    command.StartTime, command.EndTime, reservation.Id,
                    cancellationToken))
                return new(RescheduleReservationStatus.Occupied);

            if (await _blockedPeriods.HasOverlapAsync(reservation.CourtId,
                    command.StartTime, command.EndTime, cancellationToken))
                return new(RescheduleReservationStatus.Blocked);

            oldStartTime = reservation.StartTime;
            oldEndTime = reservation.EndTime;
            reservation.StartTime = command.StartTime;
            reservation.EndTime = command.EndTime;
            reservation.TotalPrice = court.PricePerHour *
                (decimal)(command.EndTime - command.StartTime).TotalHours;
            reservation.ReminderSentAtUtc = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            emailMessage = new ReservationRescheduledEmail(
                reservation.User.Email, reservation.User.FirstName, court.Name,
                oldStartTime, oldEndTime, reservation.StartTime,
                reservation.EndTime, reservation.TotalPrice, reservation.Id);
        }

        await _notifier.NotifyAvailabilityChangedAsync(
            reservation.CourtId, oldStartTime, cancellationToken);
        if (oldStartTime.Date != reservation.StartTime.Date)
        {
            await _notifier.NotifyAvailabilityChangedAsync(
                reservation.CourtId, reservation.StartTime, cancellationToken);
        }

        try
        {
            await _email.SendReservationRescheduledAsync(emailMessage, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogEmailFailure(
                exception, reservation.Id, "reschedule confirmation", warning: true);
        }

        return new(RescheduleReservationStatus.Success,
            new RescheduledReservation(reservation.Id, reservation.CourtId,
                reservation.StartTime, reservation.EndTime,
                reservation.TotalPrice, reservation.Status));
    }
}
