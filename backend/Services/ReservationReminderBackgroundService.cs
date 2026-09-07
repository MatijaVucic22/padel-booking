using Microsoft.EntityFrameworkCore;
using PadelBooking.Api.Data;

namespace PadelBooking.Api.Services;

public sealed class ReservationReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBookingTimeService _bookingTime;
    private readonly ILogger<ReservationReminderBackgroundService> _logger;

    public ReservationReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IBookingTimeService bookingTime,
        ILogger<ReservationReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _bookingTime = bookingTime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Provera reservation remindera nije uspela.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessRemindersAsync(CancellationToken cancellationToken)
    {
        var now = _bookingTime.Now;
        var reminderCutoff = now.Add(ReminderWindow);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var reservationIds = await context.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.Status != "Cancelled" &&
                reservation.StartTime > now &&
                reservation.StartTime <= reminderCutoff &&
                reservation.ReminderSentAtUtc == null)
            .OrderBy(reservation => reservation.StartTime)
            .Select(reservation => reservation.Id)
            .ToListAsync(cancellationToken);

        foreach (var reservationId in reservationIds)
        {
            try
            {
                await ProcessReservationAsync(reservationId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Slanje remindera za rezervaciju {ReservationId} nije uspelo.",
                    reservationId);
            }
        }
    }

    private async Task ProcessReservationAsync(
        int reservationId,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider
            .GetRequiredService<IEmailService>();

        var reservation = await context.Reservations
            .Include(item => item.User)
            .Include(item => item.Court)
            .FirstOrDefaultAsync(
                item => item.Id == reservationId,
                cancellationToken);

        var now = _bookingTime.Now;

        if (reservation == null ||
            reservation.Status == "Cancelled" ||
            reservation.ReminderSentAtUtc != null ||
            reservation.StartTime <= now ||
            reservation.StartTime > now.Add(ReminderWindow))
        {
            return;
        }

        await emailService.SendReservationReminderAsync(
            new ReservationReminderEmail(
                reservation.User.Email,
                reservation.User.FirstName,
                reservation.Court.Name,
                reservation.Court.Location,
                reservation.StartTime,
                reservation.EndTime,
                reservation.TotalPrice,
                reservation.Id),
            cancellationToken);

        reservation.ReminderSentAtUtc = _bookingTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}
