using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using PadelBooking.Api.Data;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Hubs;
using PadelBooking.Api.Models;
using PadelBooking.Api.Services;

namespace PadelBooking.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IBookingTimeService _bookingTime;
        private readonly ICourtAdvisoryLockService _courtLock;
        private readonly IHubContext<CourtAvailabilityHub> _availabilityHub;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            IBookingTimeService bookingTime,
            ICourtAdvisoryLockService courtLock,
            IHubContext<CourtAvailabilityHub> availabilityHub,
            ILogger<AdminController> logger)
        {
            _context = context;
            _bookingTime = bookingTime;
            _courtLock = courtLock;
            _availabilityHub = availabilityHub;
            _logger = logger;
        }

        // GET api/admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .AsNoTracking()
                .OrderByDescending(user => user.CreatedAt)
                .Select(user => new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.Role,
                    user.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET api/admin/reservations
        [HttpGet("reservations")]
        public async Task<IActionResult> GetReservations()
        {
            var reservations = await _context.Reservations
                .AsNoTracking()
                .OrderByDescending(reservation => reservation.StartTime)
                .Select(reservation => new
                {
                    reservation.Id,
                    reservation.UserId,
                    UserName = reservation.User.FirstName + " " +
                        reservation.User.LastName,
                    UserEmail = reservation.User.Email,
                    reservation.CourtId,
                    CourtName = reservation.Court.Name,
                    reservation.StartTime,
                    reservation.EndTime,
                    reservation.TotalPrice,
                    reservation.Status,
                    reservation.CreatedAt
                })
                .ToListAsync();

            return Ok(reservations);
        }

        // GET api/admin/calendar?date=yyyy-MM-dd
        [HttpGet("calendar")]
        public async Task<IActionResult> GetCalendar([FromQuery] string? date)
        {
            if (!DateOnly.TryParseExact(
                    date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var calendarDate))
            {
                return BadRequest(new
                {
                    message = "Podaci nisu ispravni.",
                    errors = new
                    {
                        date = new[] { "Datum mora biti u formatu yyyy-MM-dd." }
                    }
                });
            }

            var dayStart = calendarDate.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Unspecified);
            var dayEnd = dayStart.AddDays(1);

            var courts = await _context.Courts
                .AsNoTracking()
                .Where(court => court.IsActive)
                .OrderBy(court => court.Name)
                .Select(court => new
                {
                    court.Id,
                    court.Name
                })
                .ToListAsync();

            var reservations = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.Court.IsActive &&
                    reservation.Status != "Cancelled" &&
                    reservation.StartTime < dayEnd &&
                    reservation.EndTime > dayStart)
                .OrderBy(reservation => reservation.StartTime)
                .Select(reservation => new
                {
                    reservation.Id,
                    reservation.CourtId,
                    CourtName = reservation.Court.Name,
                    UserName = reservation.User.FirstName + " " +
                        reservation.User.LastName,
                    UserEmail = reservation.User.Email,
                    reservation.StartTime,
                    reservation.EndTime,
                    reservation.TotalPrice,
                    reservation.Status
                })
                .ToListAsync();

            var blockedPeriods = await _context.BlockedPeriods
                .AsNoTracking()
                .Where(period =>
                    period.Court.IsActive &&
                    period.StartTime < dayEnd &&
                    period.EndTime > dayStart)
                .OrderBy(period => period.StartTime)
                .Select(period => new
                {
                    period.Id,
                    period.CourtId,
                    CourtName = period.Court.Name,
                    period.StartTime,
                    period.EndTime,
                    period.Reason
                })
                .ToListAsync();

            return Ok(new
            {
                date = calendarDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                courts,
                reservations,
                blockedPeriods
            });
        }

        // POST api/admin/blocked-periods
        [HttpPost("blocked-periods")]
        public async Task<IActionResult> CreateBlockedPeriod(
            CreateBlockedPeriodRequest request)
        {
            await using var courtLock = await _courtLock.TryAcquireAsync(
                request.CourtId,
                HttpContext.RequestAborted);

            if (courtLock == null)
            {
                Response.Headers.RetryAfter = "1";
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        code = "COURT_LOCK_TIMEOUT",
                        message = "Teren je trenutno zauzet obradom drugog zahteva. Pokušajte ponovo."
                    });
            }

            var court = await _context.Courts
                .FirstOrDefaultAsync(item =>
                    item.Id == request.CourtId &&
                    item.IsActive);

            if (court == null)
            {
                return NotFound("Teren nije pronađen ili više nije aktivan.");
            }

            var overlapsReservation = await _context.Reservations
                .AnyAsync(reservation =>
                    reservation.CourtId == request.CourtId &&
                    reservation.Status != "Cancelled" &&
                    reservation.StartTime < request.EndTime &&
                    reservation.EndTime > request.StartTime);

            if (overlapsReservation)
            {
                return Conflict("Blokirani period se preklapa sa postojećom rezervacijom.");
            }

            var overlapsBlockedPeriod = await _context.BlockedPeriods
                .AnyAsync(period =>
                    period.CourtId == request.CourtId &&
                    period.StartTime < request.EndTime &&
                    period.EndTime > request.StartTime);

            if (overlapsBlockedPeriod)
            {
                return Conflict("Izabrani period je već blokiran.");
            }

            var blockedPeriod = new BlockedPeriod
            {
                CourtId = request.CourtId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Reason = request.Reason.Trim(),
                CreatedAtUtc = _bookingTime.UtcNow
            };

            _context.BlockedPeriods.Add(blockedPeriod);
            await _context.SaveChangesAsync();

            await NotifyAvailabilityChangedAsync(
                blockedPeriod.CourtId,
                blockedPeriod.StartTime);

            return Ok(new
            {
                message = "Termin je uspešno blokiran.",
                blockedPeriod = new
                {
                    blockedPeriod.Id,
                    blockedPeriod.CourtId,
                    CourtName = court.Name,
                    blockedPeriod.StartTime,
                    blockedPeriod.EndTime,
                    blockedPeriod.Reason
                }
            });
        }

        // DELETE api/admin/blocked-periods/5
        [HttpDelete("blocked-periods/{id:int}")]
        public async Task<IActionResult> DeleteBlockedPeriod(int id)
        {
            var courtId = await _context.BlockedPeriods
                .AsNoTracking()
                .Where(period => period.Id == id)
                .Select(period => (int?)period.CourtId)
                .FirstOrDefaultAsync();

            if (courtId == null)
            {
                return NotFound("Blokirani period nije pronađen.");
            }

            await using var courtLock = await _courtLock.TryAcquireAsync(
                courtId.Value,
                HttpContext.RequestAborted);

            if (courtLock == null)
            {
                Response.Headers.RetryAfter = "1";
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        code = "COURT_LOCK_TIMEOUT",
                        message = "Teren je trenutno zauzet obradom drugog zahteva. Pokušajte ponovo."
                    });
            }

            var blockedPeriod = await _context.BlockedPeriods
                .FirstOrDefaultAsync(period => period.Id == id);

            if (blockedPeriod == null)
            {
                return NotFound("Blokirani period nije pronađen.");
            }

            _context.BlockedPeriods.Remove(blockedPeriod);
            await _context.SaveChangesAsync();

            await NotifyAvailabilityChangedAsync(
                blockedPeriod.CourtId,
                blockedPeriod.StartTime);

            return Ok(new { message = "Termin je uspešno odblokiran." });
        }

        private async Task NotifyAvailabilityChangedAsync(
            int courtId,
            DateTime startTime)
        {
            try
            {
                await _availabilityHub.Clients
                    .Group(CourtAvailabilityHub.GetGroupName(courtId, startTime))
                    .SendAsync(CourtAvailabilityHub.AvailabilityChangedEvent);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "SignalR availability obaveštenje nije poslato za teren {CourtId} i datum {Date}.",
                    courtId,
                    startTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }

        // GET api/admin/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var now = _bookingTime.Now;
            var totalUsers = await _context.Users.CountAsync();
            var activeCourts = await _context.Courts
                .CountAsync(court => court.IsActive);
            var totalReservations = await _context.Reservations.CountAsync();
            var upcomingReservations = await _context.Reservations
                .CountAsync(reservation =>
                    reservation.Status != "Cancelled" &&
                    reservation.StartTime > now);
            var ongoingReservations = await _context.Reservations
                .CountAsync(reservation =>
                    reservation.Status != "Cancelled" &&
                    reservation.StartTime <= now &&
                    reservation.EndTime > now);
            var completedReservations = await _context.Reservations
                .CountAsync(reservation =>
                    reservation.Status != "Cancelled" &&
                    reservation.EndTime <= now);
            var cancelledReservations = await _context.Reservations
                .CountAsync(reservation => reservation.Status == "Cancelled");
            var realizedRevenue = await _context.Reservations
                .Where(reservation =>
                    reservation.Status != "Cancelled" &&
                    reservation.EndTime <= now)
                .SumAsync(reservation => (decimal?)reservation.TotalPrice) ?? 0;
            var upcomingRevenue = await _context.Reservations
                .Where(reservation =>
                    reservation.Status != "Cancelled" &&
                    reservation.StartTime > now)
                .SumAsync(reservation => (decimal?)reservation.TotalPrice) ?? 0;

            return Ok(new
            {
                totalUsers,
                activeCourts,
                totalReservations,
                upcomingReservations,
                ongoingReservations,
                completedReservations,
                cancelledReservations,
                realizedRevenue,
                upcomingRevenue
            });
        }
    }
}
