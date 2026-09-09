using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using PadelBooking.Api.Data;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Models;
using PadelBooking.Api.Services;
using PadelBooking.Api.Hubs;
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
        private readonly IHubContext<CourtAvailabilityHub> _availabilityHub;

        public ReservationsController(
            ApplicationDbContext context,
            IBookingTimeService bookingTime,
            ICourtAdvisoryLockService courtLock,
            IEmailService emailService,
            ILogger<ReservationsController> logger,
            IHubContext<CourtAvailabilityHub> availabilityHub)
        {
            _context = context;
            _bookingTime = bookingTime;
            _courtLock = courtLock;
            _emailService = emailService;
            _logger = logger;
            _availabilityHub = availabilityHub;
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
                        r.Status != "Cancelled" &&
                        request.StartTime < r.EndTime &&
                        request.EndTime > r.StartTime
                    );

                if (isOccupied)
                {
                    return Conflict("Izabrani termin je već zauzet.");
                }

                var isBlocked = await _context.BlockedPeriods
                    .AnyAsync(period =>
                        period.CourtId == request.CourtId &&
                        period.StartTime < request.EndTime &&
                        period.EndTime > request.StartTime);

                if (isBlocked)
                {
                    return Conflict("Izabrani termin je blokiran zbog održavanja.");
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

            await NotifyAvailabilityChangedAsync(
                reservation.CourtId,
                reservation.StartTime);

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

        [HttpPut("{id}/reschedule")]
        public async Task<IActionResult> RescheduleReservation(
            int id,
            RescheduleReservationRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var courtId = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.Id == id &&
                    reservation.UserId == userId)
                .Select(reservation => (int?)reservation.CourtId)
                .FirstOrDefaultAsync();

            if (courtId == null)
            {
                return NotFound("Rezervacija nije pronađena.");
            }

            var acquiredCourtLock = await _courtLock.TryAcquireAsync(
                courtId.Value,
                HttpContext.RequestAborted);

            if (acquiredCourtLock == null)
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

            DateTime oldStartTime;
            Reservation reservation;

            await using (acquiredCourtLock)
            {
                var lockedReservation = await _context.Reservations
                    .FirstOrDefaultAsync(item =>
                        item.Id == id &&
                        item.UserId == userId);

                if (lockedReservation == null)
                {
                    return NotFound("Rezervacija nije pronađena.");
                }

                reservation = lockedReservation;

                var now = _bookingTime.Now;

                if (reservation.Status != "Active" ||
                    reservation.StartTime <= now)
                {
                    return Conflict(
                        "Samo aktivnu buduću rezervaciju je moguće promeniti.");
                }

                var court = await _context.Courts
                    .FirstOrDefaultAsync(item =>
                        item.Id == reservation.CourtId &&
                        item.IsActive);

                if (court == null)
                {
                    return NotFound("Teren nije pronađen ili više nije aktivan.");
                }

                if (reservation.StartTime == request.StartTime &&
                    reservation.EndTime == request.EndTime)
                {
                    return Conflict("Izaberi termin različit od trenutnog.");
                }

                var isOccupied = await _context.Reservations
                    .AnyAsync(item =>
                        item.Id != reservation.Id &&
                        item.CourtId == reservation.CourtId &&
                        item.Status != "Cancelled" &&
                        request.StartTime < item.EndTime &&
                        request.EndTime > item.StartTime);

                if (isOccupied)
                {
                    return Conflict("Izabrani termin je već zauzet.");
                }

                var isBlocked = await _context.BlockedPeriods
                    .AnyAsync(period =>
                        period.CourtId == reservation.CourtId &&
                        period.StartTime < request.EndTime &&
                        period.EndTime > request.StartTime);

                if (isBlocked)
                {
                    return Conflict("Izabrani termin je blokiran zbog održavanja.");
                }

                oldStartTime = reservation.StartTime;
                reservation.StartTime = request.StartTime;
                reservation.EndTime = request.EndTime;
                reservation.ReminderSentAtUtc = null;

                await _context.SaveChangesAsync();
            }

            await NotifyAvailabilityChangedAsync(
                reservation.CourtId,
                oldStartTime);

            if (oldStartTime.Date != reservation.StartTime.Date)
            {
                await NotifyAvailabilityChangedAsync(
                    reservation.CourtId,
                    reservation.StartTime);
            }

            return Ok(new
            {
                message = "Termin rezervacije je uspešno promenjen.",
                reservation = new
                {
                    reservation.Id,
                    reservation.CourtId,
                    reservation.StartTime,
                    reservation.EndTime,
                    reservation.TotalPrice,
                    reservation.Status
                }
            });
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
                .Include(r => r.Court)
                .Include(r => r.User)
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

            await NotifyAvailabilityChangedAsync(
                reservation.CourtId,
                reservation.StartTime);

            try
            {
                await _emailService.SendReservationCancellationAsync(
                    new ReservationCancellationEmail(
                        reservation.User.Email,
                        reservation.User.FirstName,
                        reservation.Court.Name,
                        reservation.Court.Location,
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
                    "Slanje cancellation emaila za rezervaciju {ReservationId} nije uspelo.",
                    reservation.Id);
            }

            return Ok(new
            {
                message = "Rezervacija uspešno otkazana."
            });
        }

        private async Task NotifyAvailabilityChangedAsync(
            int courtId,
            DateTime reservationStartTime)
        {
            var groupName = CourtAvailabilityHub.GetGroupName(
                courtId,
                reservationStartTime);

            try
            {
                await _availabilityHub.Clients
                    .Group(groupName)
                    .SendAsync(CourtAvailabilityHub.AvailabilityChangedEvent);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "SignalR availability obaveštenje nije poslato za teren {CourtId} i datum {Date}.",
                    courtId,
                    reservationStartTime.ToString("yyyy-MM-dd"));
            }
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
                    r.Status != "Cancelled" &&
                    r.StartTime < dayEnd &&
                    r.EndTime > dayStart)
                .ToListAsync();

            var blockedPeriods = await _context.BlockedPeriods
                .Where(period =>
                    period.CourtId == courtId &&
                    period.StartTime < dayEnd &&
                    period.EndTime > dayStart)
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

                var isBlocked = blockedPeriods.Any(period =>
                    startTime < period.EndTime &&
                    endTime > period.StartTime);

                if (!isOccupied && !isBlocked && startTime > now)
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
