using Microsoft.EntityFrameworkCore;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(ApplicationDbContext context) : IPaymentRepository
{
    public void Add(Payment payment) => context.Payments.Add(payment);

    public async Task<IReadOnlyDictionary<int, decimal>> GetAppliedPaidAmountsAsync(
        IReadOnlyCollection<int> reservationIds,
        CancellationToken cancellationToken = default) =>
        await context.Payments.AsNoTracking()
            .Where(payment => reservationIds.Contains(payment.ReservationId) &&
                payment.Status == PaymentStatus.Paid &&
                payment.FulfillmentStatus == PaymentFulfillmentStatus.Applied)
            .GroupBy(payment => payment.ReservationId)
            .Select(group => new { ReservationId = group.Key, Amount = group.Sum(payment => payment.Amount) })
            .ToDictionaryAsync(item => item.ReservationId, item => item.Amount, cancellationToken);

    public Task<decimal> GetPaidCreditAsync(int reservationId, CancellationToken cancellationToken = default) =>
        context.Payments.Where(payment => payment.ReservationId == reservationId &&
                payment.Status == PaymentStatus.Paid &&
                payment.FulfillmentStatus == PaymentFulfillmentStatus.Applied)
            .SumAsync(payment => payment.Amount, cancellationToken);

    public Task<bool> HasPendingTopUpAsync(int reservationId, CancellationToken cancellationToken = default) =>
        context.Payments.AnyAsync(payment => payment.ReservationId == reservationId &&
            payment.Purpose == PaymentPurpose.RescheduleTopUp &&
            payment.Status == PaymentStatus.Pending, cancellationToken);

    // Session expiry alone cannot release a hold: Stripe may have accepted payment before
    // its webhook arrives. Reconciliation changes Pending to a terminal financial status.
    private IQueryable<Payment> LivePendingHolds(int courtId, DateTime nowLocal) =>
        context.Payments.Where(payment => payment.Reservation.CourtId == courtId &&
            payment.Status == PaymentStatus.Pending &&
            ((payment.Purpose == PaymentPurpose.InitialBooking &&
                payment.Reservation.Status == "PendingPayment" && payment.Reservation.StartTime > nowLocal) ||
             (payment.Purpose == PaymentPurpose.RescheduleTopUp &&
                payment.TargetStartTime > nowLocal && payment.TargetEndTime != null)));

    public Task<bool> HasLivePendingHoldForCourtAsync(int courtId, DateTime nowLocal,
        CancellationToken cancellationToken = default) =>
        LivePendingHolds(courtId, nowLocal).AnyAsync(cancellationToken);

    public Task<bool> HasLivePendingHoldOverlapAsync(int courtId, DateTime startTime, DateTime endTime,
        DateTime nowLocal, CancellationToken cancellationToken = default) =>
        LivePendingHolds(courtId, nowLocal).AnyAsync(payment =>
            (payment.Purpose == PaymentPurpose.InitialBooking &&
                payment.Reservation.StartTime < endTime && payment.Reservation.EndTime > startTime) ||
            (payment.Purpose == PaymentPurpose.RescheduleTopUp &&
                payment.TargetStartTime < endTime && payment.TargetEndTime > startTime), cancellationToken);

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
