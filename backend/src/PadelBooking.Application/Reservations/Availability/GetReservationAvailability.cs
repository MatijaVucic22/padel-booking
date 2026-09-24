using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Reservations.Availability;

public sealed class GetReservationAvailability
{
    private readonly ICourtRepository _courts;
    private readonly IReservationRepository _reservations;
    private readonly IBlockedPeriodRepository _blockedPeriods;
    private readonly IPaymentRepository _payments;
    private readonly IBookingTimeService _bookingTime;

    public GetReservationAvailability(ICourtRepository courts,
        IReservationRepository reservations,
        IBlockedPeriodRepository blockedPeriods,
        IPaymentRepository payments,
        IBookingTimeService bookingTime)
    {
        _courts = courts; _reservations = reservations;
        _blockedPeriods = blockedPeriods; _payments = payments; _bookingTime = bookingTime;
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
        var pendingTargets = await _payments.ListPendingTargetIntervalsAsync(
            courtId, dayStart, dayEnd, cancellationToken);
        var now = _bookingTime.Now;
        var unavailable = reservations
            .Select(item => new BookingTimeRange(item.StartTime, item.EndTime))
            .Concat(blockedPeriods.Select(item =>
                new BookingTimeRange(item.StartTime, item.EndTime)))
            .Concat(pendingTargets.Select(item =>
                new BookingTimeRange(item.StartTime, item.EndTime)));
        var slots = BookingSlotCalculator.GetAvailableStarts(
                dayStart, 1, now, unavailable)
            .Select(startTime => new AvailableReservationSlot(
                startTime, startTime.AddHours(1)))
            .ToList();

        return new(courtId, court.Name, date.Date, slots);
    }
}
