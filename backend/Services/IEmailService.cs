namespace PadelBooking.Api.Services;

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
}
