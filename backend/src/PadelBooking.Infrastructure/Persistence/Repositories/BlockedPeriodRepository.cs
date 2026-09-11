using Microsoft.EntityFrameworkCore;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Infrastructure.Persistence.Repositories;

public sealed class BlockedPeriodRepository : IBlockedPeriodRepository
{
    private readonly ApplicationDbContext _context;

    public BlockedPeriodRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<BlockedPeriod?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _context.BlockedPeriods.FirstOrDefaultAsync(
            blockedPeriod => blockedPeriod.Id == id,
            cancellationToken);

    public Task<int?> GetCourtIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _context.BlockedPeriods.AsNoTracking()
            .Where(period => period.Id == id)
            .Select(period => (int?)period.CourtId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOverlapAsync(
        int courtId,
        DateTime requestedStart,
        DateTime requestedEnd,
        CancellationToken cancellationToken = default) =>
        _context.BlockedPeriods.AnyAsync(
            blockedPeriod =>
                blockedPeriod.CourtId == courtId &&
                blockedPeriod.StartTime < requestedEnd &&
                blockedPeriod.EndTime > requestedStart,
            cancellationToken);

    public async Task<IReadOnlyList<BlockedPeriod>> ListOverlappingAsync(
        int courtId,
        DateTime requestedStart,
        DateTime requestedEnd,
        CancellationToken cancellationToken = default) =>
        await _context.BlockedPeriods
            .AsNoTracking()
            .Where(blockedPeriod =>
                blockedPeriod.CourtId == courtId &&
                blockedPeriod.StartTime < requestedEnd &&
                blockedPeriod.EndTime > requestedStart)
            .ToListAsync(cancellationToken);

    public void Add(BlockedPeriod blockedPeriod) =>
        _context.BlockedPeriods.Add(blockedPeriod);

    public void Remove(BlockedPeriod blockedPeriod) =>
        _context.BlockedPeriods.Remove(blockedPeriod);
}
