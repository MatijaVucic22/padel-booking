using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.Api.Data;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Models;

namespace PadelBooking.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CourtsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CourtsController(ApplicationDbContext context)
        {
            _context = context;
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

        // POST api/courts
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateCourt(CreateCourtRequest request)
        {
            var court = new Court
            {
                Name = request.Name.Trim(),
                Location = request.Location.Trim(),
                Description = request.Description?.Trim(),
                PricePerHour = request.PricePerHour,
                IsActive = true
            };

            _context.Courts.Add(court);
            await _context.SaveChangesAsync();

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
            var court = await _context.Courts.FindAsync(id);

            if (court == null)
            {
                return NotFound("Teren nije pronađen.");
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
