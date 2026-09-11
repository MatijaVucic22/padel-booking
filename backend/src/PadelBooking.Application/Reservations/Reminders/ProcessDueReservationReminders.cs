using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Notifications;

namespace PadelBooking.Application.Reservations.Reminders;

public sealed class ProcessDueReservationReminders
{
    private static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(3);

    private readonly IReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IBookingTimeService _bookingTime;
    private readonly IReservationNotificationLogger _logger;

    public ProcessDueReservationReminders(
        IReservationRepository reservations,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IBookingTimeService bookingTime,
        IReservationNotificationLogger logger)
    {
        _reservations = reservations;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _bookingTime = bookingTime;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = _bookingTime.Now;
        var reservationIds = await _reservations.ListDueReminderIdsAsync(
            now,
            now.Add(ReminderWindow),
            cancellationToken);

        foreach (var reservationId in reservationIds)
        {
            try
            {
                await ProcessReservationAsync(reservationId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogEmailFailure(exception, reservationId, "reminder");
            }
        }
    }

    private async Task ProcessReservationAsync(
        int reservationId,
        CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(
            reservationId,
            cancellationToken);
        var now = _bookingTime.Now;

        if (reservation is null ||
            reservation.Status == "Cancelled" ||
            reservation.ReminderSentAtUtc is not null ||
            reservation.StartTime <= now ||
            reservation.StartTime > now.Add(ReminderWindow))
        {
            return;
        }

        await _emailService.SendReservationReminderAsync(
            new ReservationReminderEmail(
                reservation.User.Email,
                reservation.User.FirstName,
                reservation.Court.Name,
                reservation.Court.Location,
                reservation.StartTime,
                reservation.EndTime,
                reservation.TotalPrice,
                reservation.Id),
            cancellationToken);

        reservation.ReminderSentAtUtc = _bookingTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
