using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Abstractions.Persistence;

public interface ICourtRepository
{
    Task<Court?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Court?> GetActiveByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Court>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Court>> ListAvailableAsync(
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default);

    void Add(Court court);
}
