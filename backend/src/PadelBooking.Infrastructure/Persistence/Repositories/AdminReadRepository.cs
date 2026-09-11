using Microsoft.EntityFrameworkCore;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Admin.Models;

namespace PadelBooking.Infrastructure.Persistence.Repositories;

public sealed class AdminReadRepository : IAdminReadRepository
{
    private readonly ApplicationDbContext _context;

    public AdminReadRepository(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<AdminUserItem>> ListUsersAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Users.AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .Select(user => new AdminUserItem(user.Id, user.FirstName,
                user.LastName, user.Email, user.Role, user.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminReservationItem>> ListReservationsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Reservations.AsNoTracking()
            .OrderByDescending(reservation => reservation.StartTime)
            .Select(reservation => new AdminReservationItem(
                reservation.Id, reservation.UserId,
                reservation.User.FirstName + " " + reservation.User.LastName,
                reservation.User.Email, reservation.CourtId, reservation.Court.Name,
                reservation.StartTime, reservation.EndTime, reservation.TotalPrice,
                reservation.Status, reservation.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<int> CountUsersAsync(CancellationToken cancellationToken = default) =>
        _context.Users.CountAsync(cancellationToken);

    public Task<int> CountActiveCourtsAsync(
        CancellationToken cancellationToken = default) =>
        _context.Courts.CountAsync(court => court.IsActive, cancellationToken);

    public async Task<IReadOnlyList<AdminReservationStatistic>>
        ListReservationStatisticsAsync(CancellationToken cancellationToken = default) =>
        await _context.Reservations.AsNoTracking()
            .Select(reservation => new AdminReservationStatistic(
                reservation.StartTime, reservation.EndTime,
                reservation.TotalPrice, reservation.Status))
            .ToListAsync(cancellationToken);

    public async Task<AdminCalendarData> GetCalendarAsync(
        DateTime dayStart, DateTime dayEnd,
        CancellationToken cancellationToken = default)
    {
        var courts = await _context.Courts.AsNoTracking()
            .Where(court => court.IsActive).OrderBy(court => court.Name)
            .Select(court => new AdminCalendarCourt(court.Id, court.Name))
            .ToListAsync(cancellationToken);
        var reservations = await _context.Reservations.AsNoTracking()
            .Where(reservation => reservation.Court.IsActive &&
                reservation.Status != "Cancelled" &&
                reservation.StartTime < dayEnd && reservation.EndTime > dayStart)
            .OrderBy(reservation => reservation.StartTime)
            .Select(reservation => new AdminCalendarReservation(
                reservation.Id, reservation.CourtId, reservation.Court.Name,
                reservation.User.FirstName + " " + reservation.User.LastName,
                reservation.User.Email, reservation.StartTime, reservation.EndTime,
                reservation.TotalPrice, reservation.Status))
            .ToListAsync(cancellationToken);
        var blockedPeriods = await _context.BlockedPeriods.AsNoTracking()
            .Where(period => period.Court.IsActive &&
                period.StartTime < dayEnd && period.EndTime > dayStart)
            .OrderBy(period => period.StartTime)
            .Select(period => new AdminCalendarBlockedPeriod(
                period.Id, period.CourtId, period.Court.Name, period.StartTime,
                period.EndTime, period.Reason))
            .ToListAsync(cancellationToken);
        return new(courts, reservations, blockedPeriods);
    }
}
