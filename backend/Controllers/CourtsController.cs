using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PadelBooking.Api.DTOs;
using PadelBooking.Application.Abstractions.Storage;
using PadelBooking.Application.Courts.CreateCourt;
using PadelBooking.Application.Courts.DeactivateCourt;
using PadelBooking.Application.Courts.GetAvailableCourts;
using PadelBooking.Application.Courts.GetCourt;
using PadelBooking.Application.Courts.GetCourts;
using PadelBooking.Application.Courts.UpdateCourt;

namespace PadelBooking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CourtsController : ControllerBase
{
    private readonly GetActiveCourts _getCourts;
    private readonly GetActiveCourt _getCourt;
    private readonly GetAvailableCourts _getAvailableCourts;
    private readonly CreateCourt _createCourt;
    private readonly UpdateCourt _updateCourt;
    private readonly DeactivateCourt _deactivateCourt;

    public CourtsController(GetActiveCourts getCourts, GetActiveCourt getCourt,
        GetAvailableCourts getAvailableCourts, CreateCourt createCourt,
        UpdateCourt updateCourt, DeactivateCourt deactivateCourt)
    {
        _getCourts = getCourts;
        _getCourt = getCourt;
        _getAvailableCourts = getAvailableCourts;
        _createCourt = createCourt;
        _updateCourt = updateCourt;
        _deactivateCourt = deactivateCourt;
    }

    [HttpGet]
    public async Task<IActionResult> GetCourts()
    {
        var courts = await _getCourts.ExecuteAsync(HttpContext.RequestAborted);
        return Ok(courts);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCourt(int id)
    {
        var court = await _getCourt.ExecuteAsync(id, HttpContext.RequestAborted);
        return court is null ? NotFound("Teren nije pronađen.") : Ok(court);
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableCourts([FromQuery] AvailableCourtsRequest request)
    {
        var courts = await _getAvailableCourts.ExecuteAsync(
            request.StartTime, request.DurationHours, HttpContext.RequestAborted);
        return Ok(courts);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateCourt([FromForm] CreateCourtRequest request)
    {
        await using Stream? imageStream = request.Image?.OpenReadStream();
        var image = request.Image is null || imageStream is null
            ? null
            : new CourtImageUpload(request.Image.FileName, request.Image.ContentType,
                request.Image.Length, imageStream);
        var court = await _createCourt.ExecuteAsync(
            new CreateCourtCommand(request.Name, request.Location, request.Description,
                request.PricePerHour, image),
            HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetCourt), new { id = court.Id }, court);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCourt(int id, UpdateCourtRequest request)
    {
        var court = await _updateCourt.ExecuteAsync(
            new UpdateCourtCommand(id, request.Name, request.Location,
                request.Description, request.PricePerHour),
            HttpContext.RequestAborted);
        return court is null ? NotFound("Teren nije pronađen.") : Ok(court);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCourt(int id)
    {
        var result = await _deactivateCourt.ExecuteAsync(id, HttpContext.RequestAborted);
        return result.Status switch
        {
            DeactivateCourtStatus.NotFound => NotFound("Teren nije pronađen."),
            DeactivateCourtStatus.HasFutureReservations => Conflict(
                "Teren nije moguće deaktivirati dok postoje aktivne buduće rezervacije."),
            DeactivateCourtStatus.LockTimeout => LockTimeout(),
            _ => Ok(new { message = "Teren je uspešno deaktiviran." })
        };
    }

    private IActionResult LockTimeout()
    {
        Response.Headers.RetryAfter = "1";
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            code = "COURT_LOCK_TIMEOUT",
            message = "Teren je trenutno zauzet obradom drugog zahteva. Pokušajte ponovo."
        });
    }
}
