using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Abstractions.Persistence;

public interface IPaymentRepository
{
    void Add(Payment payment);
    Task<decimal> GetPaidCreditAsync(int reservationId, CancellationToken cancellationToken = default);
    Task<bool> HasPendingTopUpAsync(int reservationId, CancellationToken cancellationToken = default);
    Task<bool> HasPendingTargetOverlapAsync(int courtId, DateTime startTime, DateTime endTime,
        int? excludedPaymentId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(DateTime StartTime, DateTime EndTime)>> ListPendingTargetIntervalsAsync(
        int courtId, DateTime dayStart, DateTime dayEnd, CancellationToken cancellationToken = default);
    Task<Payment?> GetTrackedBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<Payment?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListDueSessionIdsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
