using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Abstractions.Persistence;

public interface IBlockedPeriodRepository
{
    Task<BlockedPeriod?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<int?> GetCourtIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlapAsync(
        int courtId,
        DateTime requestedStart,
        DateTime requestedEnd,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BlockedPeriod>> ListOverlappingAsync(
        int courtId,
        DateTime requestedStart,
        DateTime requestedEnd,
        CancellationToken cancellationToken = default);

    void Add(BlockedPeriod blockedPeriod);

    void Remove(BlockedPeriod blockedPeriod);
}
