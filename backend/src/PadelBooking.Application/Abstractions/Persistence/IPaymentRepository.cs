using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Abstractions.Persistence;

public interface IPaymentRepository
{
    void Add(Payment payment);
    Task<Payment?> GetByReservationIdAsync(int reservationId, CancellationToken cancellationToken = default);
    Task<Payment?> GetTrackedByReservationIdAsync(int reservationId, CancellationToken cancellationToken = default);
    Task<Payment?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListDueSessionIdsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
