namespace PadelBooking.Application.Abstractions.Notifications;

public interface ICourtChangeNotifier
{
    Task NotifyCourtChangedAsync(
        int courtId,
        string changeType,
        CancellationToken cancellationToken = default);

    Task NotifyAvailabilityChangedAsync(
        int courtId,
        DateTime date,
        CancellationToken cancellationToken = default);
}
