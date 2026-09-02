using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.Api.Data;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Models;
using System.Security.Claims;

namespace PadelBooking.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReservationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReservationsController(ApplicationDbContext context)
        {
            _context = context;
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

            if (request.EndTime <= request.StartTime)
            {
                return BadRequest(
                    "Vreme završetka mora biti posle vremena početka."
                );
            }

            if (request.StartTime <= DateTime.Now)
            {
                return BadRequest(
                    "Nije moguće rezervisati termin u prošlosti."
                );
            }

            var court = await _context.Courts
                .FirstOrDefaultAsync(c =>
                    c.Id == request.CourtId &&
                    c.IsActive
                );

            if (court == null)
            {
                return NotFound("Teren nije pronađen.");
            }

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

            var reservation = new Reservation
            {
                UserId = userId,
                CourtId = court.Id,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                TotalPrice = totalPrice,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

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
                    r.Status
                })
                .ToListAsync();

            return Ok(reservations);
        }
    }
}