using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Reservations.Availability;

namespace PadelBooking.Application.Courts.GetCourts;

public sealed class GetActiveCourts
{
    private readonly ICourtRepository _courts;
    private readonly IReservationRepository _reservations;
    private readonly IBlockedPeriodRepository _blockedPeriods;
    private readonly IPaymentRepository _payments;
    private readonly IBookingTimeService _bookingTime;

    public GetActiveCourts(
        ICourtRepository courts,
        IReservationRepository reservations,
        IBlockedPeriodRepository blockedPeriods,
        IPaymentRepository payments,
        IBookingTimeService bookingTime)
    {
        _courts = courts;
        _reservations = reservations;
        _blockedPeriods = blockedPeriods;
        _payments = payments;
        _bookingTime = bookingTime;
    }

    public async Task<IReadOnlyList<ActiveCourtListItem>> ExecuteAsync(
        string? location,
        CancellationToken cancellationToken = default)
    {
        var courts = await _courts.ListActiveAsync(
            string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            cancellationToken);
        if (courts.Count == 0) return [];

        var now = _bookingTime.Now;
        var horizonStart = now.Date;
        var horizonEnd = horizonStart.AddDays(7);
        var courtIds = courts.Select(court => court.Id).ToArray();
        var reservations = await _reservations.ListOverlappingForCourtsAsync(
            courtIds, horizonStart, horizonEnd, cancellationToken);
        var blockedPeriods = await _blockedPeriods.ListOverlappingForCourtsAsync(
            courtIds, horizonStart, horizonEnd, cancellationToken);
        var pendingTargets = await _payments.ListPendingTargetIntervalsForCourtsAsync(
            courtIds, horizonStart, horizonEnd, cancellationToken);
        var unavailableByCourt = reservations
            .Concat(blockedPeriods)
            .Concat(pendingTargets)
            .ToLookup(interval => interval.CourtId);

        return courts.Select(court =>
        {
            var unavailable = unavailableByCourt[court.Id]
                .Select(interval => new BookingTimeRange(
                    interval.StartTime, interval.EndTime));
            var nextAvailableStart = BookingSlotCalculator.GetAvailableStarts(
                    horizonStart, 7, now, unavailable)
                .Select(start => (DateTime?)start)
                .FirstOrDefault();

            return new ActiveCourtListItem(
                court.Id, court.Name, court.Location, court.Description,
                court.PricePerHour, court.ImageUrl, court.IsActive,
                nextAvailableStart);
        }).ToList();
    }
}
