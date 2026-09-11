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
        CancellationToken cancellationToken = default) =>
        await _context.Courts
            .AsNoTracking()
            .Where(court => court.IsActive)
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
                    endTime > period.StartTime))
            .OrderBy(court => court.Name)
            .ToListAsync(cancellationToken);

    public void Add(Court court) => _context.Courts.Add(court);
}
