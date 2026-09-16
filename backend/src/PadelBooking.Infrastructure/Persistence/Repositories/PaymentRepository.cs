using Microsoft.EntityFrameworkCore;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(ApplicationDbContext context) : IPaymentRepository
{
    public void Add(Payment payment) => context.Payments.Add(payment);

    public Task<Payment?> GetByReservationIdAsync(int reservationId, CancellationToken cancellationToken = default) =>
        context.Payments.AsNoTracking().Include(payment => payment.Reservation)
            .ThenInclude(reservation => reservation.User)
            .Include(payment => payment.Reservation)
            .ThenInclude(reservation => reservation.Court)
            .FirstOrDefaultAsync(payment => payment.ReservationId == reservationId, cancellationToken);

    public Task<Payment?> GetTrackedByReservationIdAsync(int reservationId, CancellationToken cancellationToken = default) =>
        context.Payments.Include(payment => payment.Reservation)
            .ThenInclude(reservation => reservation.User)
            .Include(payment => payment.Reservation)
            .ThenInclude(reservation => reservation.Court)
            .FirstOrDefaultAsync(payment => payment.ReservationId == reservationId, cancellationToken);

    public Task<Payment?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default) =>
        context.Payments.AsNoTracking().Include(payment => payment.Reservation)
            .ThenInclude(reservation => reservation.User)
            .Include(payment => payment.Reservation)
            .ThenInclude(reservation => reservation.Court)
            .FirstOrDefaultAsync(payment => payment.ExternalSessionId == sessionId, cancellationToken);

    public async Task<IReadOnlyList<string>> ListDueSessionIdsAsync(DateTime nowUtc, CancellationToken cancellationToken = default) =>
        await context.Payments.AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Pending &&
                payment.ExternalSessionId != null && payment.SessionExpiresAtUtc <= nowUtc)
            .Select(payment => payment.ExternalSessionId!)
            .ToListAsync(cancellationToken);

}
