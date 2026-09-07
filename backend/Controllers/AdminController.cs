using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.Api.Data;
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

        public AdminController(
            ApplicationDbContext context,
            IBookingTimeService bookingTime)
        {
            _context = context;
            _bookingTime = bookingTime;
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
