using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Admin.Statistics;

public sealed class GetAdminStatistics
{
    private readonly IAdminReadRepository _repository;
    private readonly IBookingTimeService _bookingTime;

    public GetAdminStatistics(
        IAdminReadRepository repository,
        IBookingTimeService bookingTime)
    {
        _repository = repository;
        _bookingTime = bookingTime;
    }

    public async Task<AdminStatistics> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var totalUsers = await _repository.CountUsersAsync(cancellationToken);
        var activeCourts = await _repository.CountActiveCourtsAsync(cancellationToken);
        var reservations = await _repository.ListReservationStatisticsAsync(
            cancellationToken);
        var now = _bookingTime.Now;

        return new(totalUsers, activeCourts, reservations.Count,
            reservations.Count(item => item.Status != "Cancelled" && item.StartTime > now),
            reservations.Count(item => item.Status != "Cancelled" &&
                item.StartTime <= now && item.EndTime > now),
            reservations.Count(item => item.Status != "Cancelled" && item.EndTime <= now),
            reservations.Count(item => item.Status == "Cancelled"),
            reservations.Where(item => item.Status != "Cancelled" && item.EndTime <= now)
                .Sum(item => item.TotalPrice),
            reservations.Where(item => item.Status != "Cancelled" && item.StartTime > now)
                .Sum(item => item.TotalPrice));
    }
}
