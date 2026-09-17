using Microsoft.EntityFrameworkCore;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(ApplicationDbContext context) : IPaymentRepository
{
    public void Add(Payment payment) => context.Payments.Add(payment);

    public Task<decimal> GetPaidCreditAsync(int reservationId, CancellationToken cancellationToken = default) =>
        context.Payments.Where(payment => payment.ReservationId == reservationId &&
                payment.Status == PaymentStatus.Paid)
            .SumAsync(payment => payment.Amount, cancellationToken);

    public Task<bool> HasPendingTopUpAsync(int reservationId, CancellationToken cancellationToken = default) =>
        context.Payments.AnyAsync(payment => payment.ReservationId == reservationId &&
            payment.Purpose == PaymentPurpose.RescheduleTopUp &&
            payment.Status == PaymentStatus.Pending, cancellationToken);

    public Task<bool> HasPendingTargetOverlapAsync(int courtId, DateTime startTime, DateTime endTime,
        int? excludedPaymentId = null, CancellationToken cancellationToken = default) =>
        context.Payments.AnyAsync(payment => payment.Reservation.CourtId == courtId &&
            payment.Purpose == PaymentPurpose.RescheduleTopUp &&
            payment.Status == PaymentStatus.Pending &&
            (!excludedPaymentId.HasValue || payment.Id != excludedPaymentId.Value) &&
            payment.TargetStartTime < endTime && payment.TargetEndTime > startTime,
            cancellationToken);

    public async Task<IReadOnlyList<(DateTime StartTime, DateTime EndTime)>> ListPendingTargetIntervalsAsync(
        int courtId, DateTime dayStart, DateTime dayEnd, CancellationToken cancellationToken = default)
    {
        var intervals = await context.Payments.AsNoTracking()
            .Where(payment => payment.Reservation.CourtId == courtId &&
                payment.Purpose == PaymentPurpose.RescheduleTopUp &&
                payment.Status == PaymentStatus.Pending &&
                payment.TargetStartTime < dayEnd && payment.TargetEndTime > dayStart)
            .Select(payment => new { payment.TargetStartTime, payment.TargetEndTime })
            .ToListAsync(cancellationToken);
        return intervals.Select(interval => (interval.TargetStartTime!.Value, interval.TargetEndTime!.Value)).ToList();
    }

    public Task<Payment?> GetTrackedBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default) =>
        context.Payments.Include(payment => payment.Reservation)
            .ThenInclude(reservation => reservation.User)
            .Include(payment => payment.Reservation)
            .ThenInclude(reservation => reservation.Court)
            .FirstOrDefaultAsync(payment => payment.ExternalSessionId == sessionId, cancellationToken);

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
