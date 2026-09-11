using PadelBooking.Application.Admin.Models;

namespace PadelBooking.Application.Abstractions.Persistence;

public interface IAdminReadRepository
{
    Task<IReadOnlyList<AdminUserItem>> ListUsersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminReservationItem>> ListReservationsAsync(
        CancellationToken cancellationToken = default);

    Task<int> CountUsersAsync(CancellationToken cancellationToken = default);

    Task<int> CountActiveCourtsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminReservationStatistic>> ListReservationStatisticsAsync(
        CancellationToken cancellationToken = default);

    Task<AdminCalendarData> GetCalendarAsync(
        DateTime dayStart,
        DateTime dayEnd,
        CancellationToken cancellationToken = default);
}
