using PadelBooking.Application.Abstractions.Notifications;

namespace PadelBooking.Api.Services;

public sealed class ReservationNotificationLogger : IReservationNotificationLogger
{
    private readonly ILogger<ReservationNotificationLogger> _logger;

    public ReservationNotificationLogger(
        ILogger<ReservationNotificationLogger> logger) => _logger = logger;

    public void LogEmailFailure(
        Exception exception,
        int reservationId,
        string notificationType,
        bool warning = false)
    {
        if (warning)
        {
            _logger.LogWarning(exception,
                "Slanje {NotificationType} emaila za rezervaciju {ReservationId} nije uspelo.",
                notificationType, reservationId);
            return;
        }

        _logger.LogError(exception,
            "Slanje {NotificationType} emaila za rezervaciju {ReservationId} nije uspelo.",
            notificationType, reservationId);
    }
}
