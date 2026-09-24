using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Reservations.Availability;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Courts.GetAvailableCourts;

public sealed class GetAvailableCourts
{
    private readonly ICourtRepository _courts;
    private readonly IBookingTimeService _bookingTime;

    public GetAvailableCourts(ICourtRepository courts, IBookingTimeService bookingTime)
    {
        _courts = courts;
        _bookingTime = bookingTime;
    }

    public async Task<IReadOnlyList<Court>> ExecuteAsync(
        DateTime startTime,
        int durationHours,
        CancellationToken cancellationToken = default)
    {
        if (!BookingCutoffPolicy.CanBook(startTime, _bookingTime.Now)) return [];

        return await _courts.ListAvailableAsync(
            startTime,
            startTime.AddHours(durationHours),
            cancellationToken);
    }
}
