namespace PadelBooking.Application.Abstractions.Notifications;

public interface IReservationNotificationLogger
{
    void LogEmailFailure(
        Exception exception,
        int reservationId,
        string notificationType,
        bool warning = false);
}
