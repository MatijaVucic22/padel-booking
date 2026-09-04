using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.Api.Data;

namespace PadelBooking.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
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
            var totalUsers = await _context.Users.CountAsync();
            var activeCourts = await _context.Courts
                .CountAsync(court => court.IsActive);
            var totalReservations = await _context.Reservations.CountAsync();
            var activeReservations = await _context.Reservations
                .CountAsync(reservation => reservation.Status == "Active");
            var totalRevenue = await _context.Reservations
                .Where(reservation => reservation.Status == "Active")
                .SumAsync(reservation => (decimal?)reservation.TotalPrice) ?? 0;

            return Ok(new
            {
                totalUsers,
                activeCourts,
                totalReservations,
                activeReservations,
                totalRevenue
            });
        }
    }
}
