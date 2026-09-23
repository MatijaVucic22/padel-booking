using Microsoft.EntityFrameworkCore;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Infrastructure.Persistence.Repositories;

public sealed class CourtRepository : ICourtRepository
{
    private readonly ApplicationDbContext _context;

    public CourtRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Court?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _context.Courts.FirstOrDefaultAsync(
            court => court.Id == id,
            cancellationToken);

    public Task<Court?> GetActiveByIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _context.Courts.FirstOrDefaultAsync(
            court => court.Id == id && court.IsActive,
            cancellationToken);

    public async Task<IReadOnlyList<Court>> ListActiveAsync(
        string? location = null,
        CancellationToken cancellationToken = default) =>
        await _context.Courts
            .AsNoTracking()
            .Where(court => court.IsActive &&
                (location == null || court.Location.Contains(location)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Court>> ListAvailableAsync(
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default) =>
        await _context.Courts
            .AsNoTracking()
            .Where(court =>
                court.IsActive &&
                !_context.Reservations.Any(reservation =>
                    reservation.CourtId == court.Id &&
                    reservation.Status != "Cancelled" &&
                    startTime < reservation.EndTime &&
                    endTime > reservation.StartTime) &&
                !_context.BlockedPeriods.Any(period =>
                    period.CourtId == court.Id &&
                    startTime < period.EndTime &&
                    endTime > period.StartTime) &&
                !_context.Payments.Any(payment =>
                    payment.Reservation.CourtId == court.Id &&
                    payment.Purpose == PaymentPurpose.RescheduleTopUp &&
                    payment.Status == PaymentStatus.Pending &&
                    payment.TargetStartTime < endTime &&
                    payment.TargetEndTime > startTime))
            .OrderBy(court => court.Name)
            .ToListAsync(cancellationToken);

    public void Add(Court court) => _context.Courts.Add(court);
}
