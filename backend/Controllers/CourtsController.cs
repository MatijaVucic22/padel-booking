using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.Api.Data;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Models;
using PadelBooking.Api.Services;

namespace PadelBooking.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CourtsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IBookingTimeService _bookingTime;
        private readonly ICourtAdvisoryLockService _courtLock;
        private readonly IWebHostEnvironment _environment;

        public CourtsController(
            ApplicationDbContext context,
            IBookingTimeService bookingTime,
            ICourtAdvisoryLockService courtLock,
            IWebHostEnvironment environment)
        {
            _context = context;
            _bookingTime = bookingTime;
            _courtLock = courtLock;
            _environment = environment;
        }

        // GET api/courts
        [HttpGet]
        public async Task<IActionResult> GetCourts()
        {
            var courts = await _context.Courts
                .Where(c => c.IsActive)
                .ToListAsync();

            return Ok(courts);
        }

        // GET api/courts/1
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCourt(int id)
        {
            var court = await _context.Courts
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (court == null)
            {
                return NotFound("Teren nije pronađen.");
            }

            return Ok(court);
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableCourts(
            [FromQuery] AvailableCourtsRequest request)
        {
            var endTime = request.StartTime.AddHours(request.DurationHours);

            var courts = await _context.Courts
                .Where(court =>
                    court.IsActive &&
                    !_context.Reservations.Any(reservation =>
                        reservation.CourtId == court.Id &&
                        reservation.Status != "Cancelled" &&
                        request.StartTime < reservation.EndTime &&
                        endTime > reservation.StartTime) &&
                    !_context.BlockedPeriods.Any(period =>
                        period.CourtId == court.Id &&
                        request.StartTime < period.EndTime &&
                        endTime > period.StartTime))
                .OrderBy(court => court.Name)
                .ToListAsync();

            return Ok(courts);
        }

        // POST api/courts
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateCourt([FromForm] CreateCourtRequest request)
        {
            string? storedImagePath = null;
            string? imageUrl = null;

            if (request.Image != null)
            {
                var uploadsDirectory = Path.Combine(
                    _environment.WebRootPath ??
                        Path.Combine(_environment.ContentRootPath, "wwwroot"),
                    "uploads",
                    "courts");

                Directory.CreateDirectory(uploadsDirectory);

                var extension = request.Image.ContentType.ToLowerInvariant() switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => throw new InvalidOperationException(
                        "Nepodržan tip slike prošao je validaciju.")
                };
                var generatedFileName = $"{Guid.NewGuid():N}{extension}";
                storedImagePath = Path.Combine(uploadsDirectory, generatedFileName);
                imageUrl = $"/uploads/courts/{generatedFileName}";

                try
                {
                    await using var imageStream = new FileStream(
                        storedImagePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None);
                    await request.Image.CopyToAsync(
                        imageStream,
                        HttpContext.RequestAborted);
                }
                catch
                {
                    if (System.IO.File.Exists(storedImagePath))
                    {
                        System.IO.File.Delete(storedImagePath);
                    }

                    throw;
                }
            }

            var court = new Court
            {
                Name = request.Name.Trim(),
                Location = request.Location.Trim(),
                Description = request.Description?.Trim(),
                PricePerHour = request.PricePerHour,
                ImageUrl = imageUrl,
                IsActive = true
            };

            try
            {
                _context.Courts.Add(court);
                await _context.SaveChangesAsync();
            }
            catch
            {
                if (storedImagePath != null && System.IO.File.Exists(storedImagePath))
                {
                    System.IO.File.Delete(storedImagePath);
                }

                throw;
            }

            return CreatedAtAction(
                nameof(GetCourt),
                new { id = court.Id },
                court
            );
        }

        // PUT api/courts/4
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourt(
            int id,
            UpdateCourtRequest request)
        {
            var court = await _context.Courts.FindAsync(id);

            if (court == null)
            {
                return NotFound("Teren nije pronađen.");
            }

            court.Name = request.Name.Trim();
            court.Location = request.Location.Trim();
            court.Description = request.Description?.Trim();
            court.PricePerHour = request.PricePerHour;

            await _context.SaveChangesAsync();

            return Ok(court);
        }

        // DELETE api/courts/4
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourt(int id)
        {
            await using var courtLock = await _courtLock.TryAcquireAsync(
                id,
                HttpContext.RequestAborted
            );

            if (courtLock == null)
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

            var court = await _context.Courts.FindAsync(id);

            if (court == null)
            {
                return NotFound("Teren nije pronađen.");
            }

            var hasFutureReservations = await _context.Reservations
                .AnyAsync(reservation =>
                    reservation.CourtId == id &&
                    reservation.Status == "Active" &&
                    reservation.StartTime > _bookingTime.Now
                );

            if (hasFutureReservations)
            {
                return Conflict(
                    "Teren nije moguće deaktivirati dok postoje aktivne buduće rezervacije."
                );
            }

            court.IsActive = false;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Teren je uspešno deaktiviran."
            });
        }
    }
}
