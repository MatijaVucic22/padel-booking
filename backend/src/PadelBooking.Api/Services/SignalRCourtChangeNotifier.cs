using Microsoft.AspNetCore.SignalR;
using PadelBooking.Api.Hubs;
using PadelBooking.Application.Abstractions.Notifications;

namespace PadelBooking.Api.Services;

public sealed class SignalRCourtChangeNotifier : ICourtChangeNotifier
{
    private readonly IHubContext<CourtAvailabilityHub> _hub;
    private readonly ILogger<SignalRCourtChangeNotifier> _logger;

    public SignalRCourtChangeNotifier(
        IHubContext<CourtAvailabilityHub> hub,
        ILogger<SignalRCourtChangeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyCourtChangedAsync(
        int courtId,
        string changeType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.All.SendAsync(
                CourtAvailabilityHub.CourtChangedEvent,
                new { courtId, changeType },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Court change notification failed for court {CourtId}.",
                courtId);
        }
    }

    public async Task NotifyAvailabilityChangedAsync(
        int courtId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients
                .Group(CourtAvailabilityHub.GetGroupName(courtId, date))
                .SendAsync(
                    CourtAvailabilityHub.AvailabilityChangedEvent,
                    cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "SignalR availability obaveštenje nije poslato za teren {CourtId} i datum {Date}.",
                courtId,
                date.ToString("yyyy-MM-dd"));
        }
    }
}
