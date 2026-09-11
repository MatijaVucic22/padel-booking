using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Reservations.Availability;

public sealed class GetReservationAvailability
{
    private readonly ICourtRepository _courts;
    private readonly IReservationRepository _reservations;
    private readonly IBlockedPeriodRepository _blockedPeriods;
    private readonly IBookingTimeService _bookingTime;

    public GetReservationAvailability(ICourtRepository courts,
        IReservationRepository reservations,
        IBlockedPeriodRepository blockedPeriods,
        IBookingTimeService bookingTime)
    {
        _courts = courts; _reservations = reservations;
        _blockedPeriods = blockedPeriods; _bookingTime = bookingTime;
    }

    public async Task<ReservationAvailability?> ExecuteAsync(
        int courtId,
        DateTime date,
        int? reservationId,
        int? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var court = await _courts.GetActiveByIdAsync(courtId, cancellationToken);
        if (court is null) return null;

        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        int? excludedReservationId = null;
        if (reservationId.HasValue && currentUserId.HasValue)
        {
            var ownedCourtId = await _reservations.GetCourtIdForUserAsync(
                reservationId.Value, currentUserId.Value, cancellationToken);
            if (ownedCourtId == courtId) excludedReservationId = reservationId;
        }

        var reservations = await _reservations.ListOverlappingAsync(
            courtId, dayStart, dayEnd, excludedReservationId, cancellationToken);
        var blockedPeriods = await _blockedPeriods.ListOverlappingAsync(
            courtId, dayStart, dayEnd, cancellationToken);
        var slots = new List<AvailableReservationSlot>();
        var now = _bookingTime.Now;

        for (var hour = 8; hour < 22; hour++)
        {
            var startTime = dayStart.AddHours(hour);
            var endTime = startTime.AddHours(1);
            var occupied = reservations.Any(item =>
                startTime < item.EndTime && endTime > item.StartTime);
            var blocked = blockedPeriods.Any(item =>
                startTime < item.EndTime && endTime > item.StartTime);
            if (!occupied && !blocked && startTime > now)
                slots.Add(new(startTime, endTime));
        }

        return new(courtId, court.Name, date.Date, slots);
    }
}
