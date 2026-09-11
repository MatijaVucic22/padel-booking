using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Notifications;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Reservations.Create;

public sealed class CreateReservation
{
    private readonly IUserRepository _users;
    private readonly ICourtRepository _courts;
    private readonly IReservationRepository _reservations;
    private readonly IBlockedPeriodRepository _blockedPeriods;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourtAdvisoryLockService _courtLock;
    private readonly IBookingTimeService _bookingTime;
    private readonly IEmailService _email;
    private readonly ICourtChangeNotifier _notifier;
    private readonly IReservationNotificationLogger _logger;

    public CreateReservation(IUserRepository users, ICourtRepository courts,
        IReservationRepository reservations, IBlockedPeriodRepository blockedPeriods,
        IUnitOfWork unitOfWork, ICourtAdvisoryLockService courtLock,
        IBookingTimeService bookingTime, IEmailService email,
        ICourtChangeNotifier notifier, IReservationNotificationLogger logger)
    {
        _users = users; _courts = courts; _reservations = reservations;
        _blockedPeriods = blockedPeriods; _unitOfWork = unitOfWork;
        _courtLock = courtLock; _bookingTime = bookingTime; _email = email;
        _notifier = notifier; _logger = logger;
    }

    public async Task<CreateReservationResult> ExecuteAsync(
        CreateReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null) return new(CreateReservationStatus.Unauthorized);

        var acquiredLock = await _courtLock.TryAcquireAsync(
            command.CourtId, cancellationToken);
        if (acquiredLock is null) return new(CreateReservationStatus.LockTimeout);

        Reservation reservation;
        Court court;
        await using (acquiredLock)
        {
            court = (await _courts.GetActiveByIdAsync(
                command.CourtId, cancellationToken))!;
            if (court is null) return new(CreateReservationStatus.CourtNotFound);

            if (await _reservations.HasOverlapAsync(command.CourtId,
                    command.StartTime, command.EndTime, null, cancellationToken))
                return new(CreateReservationStatus.Occupied);

            if (await _blockedPeriods.HasOverlapAsync(command.CourtId,
                    command.StartTime, command.EndTime, cancellationToken))
                return new(CreateReservationStatus.Blocked);

            var durationHours = (decimal)(command.EndTime - command.StartTime).TotalHours;
            reservation = new Reservation
            {
                UserId = command.UserId,
                CourtId = court.Id,
                StartTime = command.StartTime,
                EndTime = command.EndTime,
                TotalPrice = Math.Round(court.PricePerHour * durationHours, 2),
                Status = "Active",
                CreatedAt = _bookingTime.UtcNow
            };
            _reservations.Add(reservation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _notifier.NotifyAvailabilityChangedAsync(
            reservation.CourtId, reservation.StartTime, cancellationToken);
        try
        {
            await _email.SendReservationConfirmationAsync(
                new ReservationConfirmationEmail(user.Email, user.FirstName,
                    court.Name, court.Location, reservation.StartTime,
                    reservation.EndTime, reservation.TotalPrice, reservation.Id),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogEmailFailure(exception, reservation.Id, "confirmation");
        }

        return new(CreateReservationStatus.Success,
            new CreatedReservation(reservation.Id, reservation.CourtId, court.Name,
                reservation.StartTime, reservation.EndTime, reservation.TotalPrice,
                reservation.Status));
    }
}
