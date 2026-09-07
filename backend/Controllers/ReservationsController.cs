using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.Api.Data;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Models;
using PadelBooking.Api.Services;
using System.Security.Claims;

namespace PadelBooking.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReservationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IBookingTimeService _bookingTime;
        private readonly ICourtAdvisoryLockService _courtLock;
        private readonly IEmailService _emailService;
        private readonly ILogger<ReservationsController> _logger;

        public ReservationsController(
            ApplicationDbContext context,
            IBookingTimeService bookingTime,
            ICourtAdvisoryLockService courtLock,
            IEmailService emailService,
            ILogger<ReservationsController> logger)
        {
            _context = context;
            _bookingTime = bookingTime;
            _courtLock = courtLock;
            _emailService = emailService;
            _logger = logger;
        }

        // POST api/reservations
        [HttpPost]
        public async Task<IActionResult> CreateReservation(
            CreateReservationRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Unauthorized();
            }

            var acquiredCourtLock = await _courtLock.TryAcquireAsync(
                request.CourtId,
                HttpContext.RequestAborted
            );

            if (acquiredCourtLock == null)
            {
                Response.Headers.RetryAfter = "1";
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        code = "COURT_LOCK_TIMEOUT",
                        message = "Teren je trenutno zauzet obradom drugog zahteva. Pokušajte ponovo."
                    }
                );
            }

            Reservation reservation;
            Court court;

            await using (acquiredCourtLock)
            {
                var lockedCourt = await _context.Courts
                    .FirstOrDefaultAsync(c =>
                        c.Id == request.CourtId &&
                        c.IsActive
                    );

                if (lockedCourt == null)
                {
                    return NotFound("Teren nije pronađen ili više nije aktivan.");
                }

                court = lockedCourt;

                var isOccupied = await _context.Reservations
                    .AnyAsync(r =>
                        r.CourtId == request.CourtId &&
                        r.Status == "Active" &&
                        request.StartTime < r.EndTime &&
                        request.EndTime > r.StartTime
                    );

                if (isOccupied)
                {
                    return Conflict("Izabrani termin je već zauzet.");
                }

                var durationHours =
                    (decimal)(request.EndTime - request.StartTime).TotalHours;

                var totalPrice = Math.Round(
                    court.PricePerHour * durationHours,
                    2
                );

                reservation = new Reservation
                {
                    UserId = userId,
                    CourtId = court.Id,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    TotalPrice = totalPrice,
                    Status = "Active",
                    CreatedAt = _bookingTime.UtcNow
                };

                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();
            }

            try
            {
                await _emailService.SendReservationConfirmationAsync(
                    new ReservationConfirmationEmail(
                        user.Email,
                        user.FirstName,
                        court.Name,
                        court.Location,
                        reservation.StartTime,
                        reservation.EndTime,
                        reservation.TotalPrice,
                        reservation.Id),
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Slanje confirmation emaila za rezervaciju {ReservationId} nije uspelo.",
                    reservation.Id);
            }

            return Ok(new
            {
                message = "Rezervacija uspešno kreirana.",
                reservation = new
                {
                    reservation.Id,
                    reservation.CourtId,
                    court.Name,
                    reservation.StartTime,
                    reservation.EndTime,
                    reservation.TotalPrice,
                    reservation.Status
                }
            });
        }

        // GET api/reservations/my
        [HttpGet("my")]
        public async Task<IActionResult> GetMyReservations()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var now = _bookingTime.Now;
            var reservations = await _context.Reservations
                .Where(r => r.UserId == userId)
                .Include(r => r.Court)
                .OrderBy(r => r.StartTime)
                .Select(r => new
                {
                    r.Id,
                    r.CourtId,
                    CourtName = r.Court.Name,
                    r.StartTime,
                    r.EndTime,
                    r.TotalPrice,
                    CanCancel = r.Status != "Cancelled" && r.StartTime > now,
                    r.Status
                })
                .ToListAsync();

            return Ok(reservations);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelReservation(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r =>
                    r.Id == id &&
                    r.UserId == userId
                );

            if (reservation == null)
            {
                return NotFound("Rezervacija nije pronađena.");
            }

            if (reservation.Status == "Cancelled")
            {
                return Conflict("Rezervacija je već otkazana.");
            }

            var now = _bookingTime.Now;

            if (reservation.EndTime <= now)
            {
                return Conflict("Završenu rezervaciju nije moguće otkazati.");
            }

            if (reservation.StartTime <= now)
            {
                return Conflict(
                    "Rezervaciju koja je već počela nije moguće otkazati."
                );
            }

            reservation.Status = "Cancelled";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Rezervacija uspešno otkazana."
            });
        }
        [AllowAnonymous]
        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableSlots(
            int courtId,
            DateTime date)
        {
            var court = await _context.Courts
                .FirstOrDefaultAsync(c => c.Id == courtId && c.IsActive);

            if (court == null)
            {
                return NotFound("Teren nije pronađen.");
            }

            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            var reservations = await _context.Reservations
                .Where(r =>
                    r.CourtId == courtId &&
                    r.Status == "Active" &&
                    r.StartTime < dayEnd &&
                    r.EndTime > dayStart)
                .ToListAsync();

            var availableSlots = new List<object>();
            var now = _bookingTime.Now;

            for (int hour = 8; hour < 22; hour++)
            {
                var startTime = dayStart.AddHours(hour);
                var endTime = startTime.AddHours(1);

                var isOccupied = reservations.Any(r =>
                    startTime < r.EndTime &&
                    endTime > r.StartTime
                );

                if (!isOccupied && startTime > now)
                {
                    availableSlots.Add(new
                    {
                        startTime,
                        endTime
                    });
                }
            }

            return Ok(new
            {
                courtId,
                courtName = court.Name,
                date = date.Date,
                slots = availableSlots
            });
        }

    }
}
