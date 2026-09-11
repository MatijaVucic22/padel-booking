using PadelBooking.Application.Notifications;

namespace PadelBooking.Application.Abstractions.Notifications;

public interface IEmailService
{
    Task SendReservationConfirmationAsync(
        ReservationConfirmationEmail confirmation,
        CancellationToken cancellationToken = default
    );

    Task SendReservationCancellationAsync(
        ReservationCancellationEmail cancellation,
        CancellationToken cancellationToken = default
    );

    Task SendReservationReminderAsync(
        ReservationReminderEmail reminder,
        CancellationToken cancellationToken = default
    );

    Task SendReservationRescheduledAsync(
        ReservationRescheduledEmail rescheduled,
        CancellationToken cancellationToken = default
    );
}
