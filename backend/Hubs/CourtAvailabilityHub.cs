using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace PadelBooking.Api.Hubs;

public sealed class CourtAvailabilityHub : Hub
{
    public const string AvailabilityChangedEvent = "AvailabilityChanged";
    public const string CourtChangedEvent = "CourtChanged";

    public Task JoinCourtDate(int courtId, string date)
    {
        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetGroupName(courtId, ParseDate(courtId, date)));
    }

    public Task LeaveCourtDate(int courtId, string date)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetGroupName(courtId, ParseDate(courtId, date)));
    }

    public static string GetGroupName(int courtId, DateTime date)
    {
        if (courtId <= 0)
        {
            throw new HubException("Court ID nije ispravan.");
        }

        return $"court:{courtId}:{date:yyyy-MM-dd}";
    }

    private static DateTime ParseDate(int courtId, string date)
    {
        if (courtId <= 0 ||
            !DateTime.TryParseExact(
                date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            throw new HubException("Court ID ili datum nisu ispravni.");
        }

        return parsedDate;
    }
}
