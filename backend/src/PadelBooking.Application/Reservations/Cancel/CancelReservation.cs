using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Notifications;

namespace PadelBooking.Application.Reservations.Cancel;

public sealed class CancelReservation
{
    private readonly IReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBookingTimeService _bookingTime;
    private readonly IEmailService _email;
    private readonly ICourtChangeNotifier _notifier;
    private readonly IReservationNotificationLogger _logger;

    public CancelReservation(IReservationRepository reservations,
        IUnitOfWork unitOfWork, IBookingTimeService bookingTime,
        IEmailService email, ICourtChangeNotifier notifier,
        IReservationNotificationLogger logger)
    {
        _reservations = reservations; _unitOfWork = unitOfWork;
        _bookingTime = bookingTime; _email = email;
        _notifier = notifier; _logger = logger;
    }

    public async Task<CancelReservationResult> ExecuteAsync(
        int id, int userId, CancellationToken cancellationToken = default)
    {
        var reservation = await _reservations.GetByIdForUserAsync(
            id, userId, cancellationToken);
        if (reservation is null) return new(CancelReservationStatus.NotFound);
        if (reservation.Status == "Cancelled")
            return new(CancelReservationStatus.AlreadyCancelled);

        var now = _bookingTime.Now;
        if (reservation.EndTime <= now) return new(CancelReservationStatus.Completed);
        if (reservation.StartTime <= now) return new(CancelReservationStatus.Started);

        reservation.Status = "Cancelled";
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
