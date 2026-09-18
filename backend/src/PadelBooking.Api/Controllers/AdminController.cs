using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Errors;
using PadelBooking.Application.Admin.BlockedPeriods;
using PadelBooking.Application.Admin.Calendar;
using PadelBooking.Application.Admin.Reservations;
using PadelBooking.Application.Admin.Statistics;
using PadelBooking.Application.Admin.Users;

namespace PadelBooking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly GetAdminUsers _getUsers;
    private readonly GetAdminReservations _getReservations;
    private readonly GetAdminStatistics _getStatistics;
    private readonly GetAdminCalendar _getCalendar;
    private readonly CreateBlockedPeriod _createBlockedPeriod;
    private readonly DeleteBlockedPeriod _deleteBlockedPeriod;

    public AdminController(GetAdminUsers getUsers,
        GetAdminReservations getReservations, GetAdminStatistics getStatistics,
        GetAdminCalendar getCalendar, CreateBlockedPeriod createBlockedPeriod,
        DeleteBlockedPeriod deleteBlockedPeriod)
    {
        _getUsers = getUsers; _getReservations = getReservations;
        _getStatistics = getStatistics; _getCalendar = getCalendar;
        _createBlockedPeriod = createBlockedPeriod;
        _deleteBlockedPeriod = deleteBlockedPeriod;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() =>
        Ok(await _getUsers.ExecuteAsync(HttpContext.RequestAborted));

    [HttpGet("reservations")]
    public async Task<IActionResult> GetReservations() =>
        Ok(await _getReservations.ExecuteAsync(HttpContext.RequestAborted));

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] string? date)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var calendarDate))
        {
            return ApiProblem.Validation(HttpContext, new Dictionary<string, string[]>
            {
                ["date"] = ["Datum mora biti u formatu yyyy-MM-dd."]
            });
        }

        var result = await _getCalendar.ExecuteAsync(
            calendarDate, HttpContext.RequestAborted);
        return Ok(new
        {
            date = result.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            result.Data.Courts,
            result.Data.Reservations,
            result.Data.BlockedPeriods
        });
    }

    [HttpPost("blocked-periods")]
    public async Task<IActionResult> CreateBlockedPeriod(
        CreateBlockedPeriodRequest request)
    {
        var result = await _createBlockedPeriod.ExecuteAsync(
            new CreateBlockedPeriodCommand(request.CourtId, request.StartTime,
                request.EndTime, request.Reason),
            HttpContext.RequestAborted);
        return result.Status switch
        {
            CreateBlockedPeriodStatus.CourtNotFound => this.ApiError(404, ApiErrorCodes.CourtInactive,
                "Teren nije pronađen ili više nije aktivan."),
            CreateBlockedPeriodStatus.ReservationOverlap => this.ApiError(409, ApiErrorCodes.SlotUnavailable,
                "Blokirani period se preklapa sa postojećom rezervacijom."),
            CreateBlockedPeriodStatus.PaymentHoldOverlap => this.ApiError(409, ApiErrorCodes.ActivePaymentHold,
                "Blokiranje nije moguće dok traje plaćanje za ovaj termin."),
            CreateBlockedPeriodStatus.BlockedPeriodOverlap => this.ApiError(409, ApiErrorCodes.SlotUnavailable,
                "Izabrani period je već blokiran."),
            CreateBlockedPeriodStatus.LockTimeout => LockTimeout(),
            _ => Ok(new
            {
                message = "Termin je uspešno blokiran.",
                blockedPeriod = result.BlockedPeriod
            })
        };
    }

    [HttpDelete("blocked-periods/{id:int}")]
    public async Task<IActionResult> DeleteBlockedPeriod(int id)
    {
        var result = await _deleteBlockedPeriod.ExecuteAsync(
            id, HttpContext.RequestAborted);
        return result.Status switch
        {
            DeleteBlockedPeriodStatus.NotFound => this.ApiError(404, ApiErrorCodes.NotFound,
                "Blokirani period nije pronađen."),
            DeleteBlockedPeriodStatus.LockTimeout => LockTimeout(),
            _ => Ok(new { message = "Termin je uspešno odblokiran." })
        };
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats() =>
        Ok(await _getStatistics.ExecuteAsync(HttpContext.RequestAborted));

    private IActionResult LockTimeout()
    {
        Response.Headers.RetryAfter = "1";
        return this.ApiError(503, ApiErrorCodes.CourtLockTimeout,
            "Teren je trenutno zauzet obradom drugog zahteva. Pokušajte ponovo.");
    }
}
