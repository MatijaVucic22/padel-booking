using Microsoft.EntityFrameworkCore;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Infrastructure.Persistence.Repositories;

public sealed class ReservationRepository : IReservationRepository
{
    private readonly ApplicationDbContext _context;

    public ReservationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Reservation?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _context.Reservations
            .Include(reservation => reservation.User)
            .Include(reservation => reservation.Court)
            .FirstOrDefaultAsync(
                reservation => reservation.Id == id,
                cancellationToken);

    public Task<Reservation?> GetByIdForUserAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default) =>
        _context.Reservations
            .Include(reservation => reservation.User)
            .Include(reservation => reservation.Court)
            .FirstOrDefaultAsync(
                reservation => reservation.Id == id &&
                    reservation.UserId == userId,
                cancellationToken);

    public Task<int?> GetCourtIdForUserAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default) =>
        _context.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.Id == id && reservation.UserId == userId)
            .Select(reservation => (int?)reservation.CourtId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Reservation>> GetForUserAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        await _context.Reservations
            .AsNoTracking()
            .Include(reservation => reservation.Court)
            .Where(reservation => reservation.UserId == userId)
            .OrderBy(reservation => reservation.StartTime)
            .ToListAsync(cancellationToken);

    public Task<bool> HasOverlapAsync(
        int courtId,
        DateTime requestedStart,
        DateTime requestedEnd,
        int? excludedReservationId = null,
        CancellationToken cancellationToken = default) =>
        _context.Reservations.AnyAsync(
            reservation =>
                reservation.CourtId == courtId &&
                reservation.Status != "Cancelled" &&
                (!excludedReservationId.HasValue ||
                    reservation.Id != excludedReservationId.Value) &&
                reservation.StartTime < requestedEnd &&
                reservation.EndTime > requestedStart,
            cancellationToken);

    public Task<bool> HasFutureActiveReservationsAsync(
        int courtId,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        _context.Reservations.AnyAsync(
            reservation =>
                reservation.CourtId == courtId &&
                reservation.Status == "Active" &&
                reservation.StartTime > now,
            cancellationToken);

    public async Task<IReadOnlyList<Reservation>> ListOverlappingAsync(
        int courtId,
        DateTime requestedStart,
        DateTime requestedEnd,
        int? excludedReservationId = null,
        CancellationToken cancellationToken = default) =>
        await _context.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.CourtId == courtId &&
                reservation.Status != "Cancelled" &&
                (!excludedReservationId.HasValue ||
                    reservation.Id != excludedReservationId.Value) &&
                reservation.StartTime < requestedEnd &&
                reservation.EndTime > requestedStart)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> ListDueReminderIdsAsync(
        DateTime now,
        DateTime reminderCutoff,
        CancellationToken cancellationToken = default) =>
        await _context.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.Status != "Cancelled" &&
                reservation.StartTime > now &&
                reservation.StartTime <= reminderCutoff &&
                reservation.ReminderSentAtUtc == null)
            .OrderBy(reservation => reservation.StartTime)
            .Select(reservation => reservation.Id)
            .ToListAsync(cancellationToken);

    public void Add(Reservation reservation) =>
        _context.Reservations.Add(reservation);
}
