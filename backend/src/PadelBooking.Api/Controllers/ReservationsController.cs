using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PadelBooking.Api.DTOs;
using PadelBooking.Application.Reservations.Availability;
using PadelBooking.Application.Reservations.Cancel;
using PadelBooking.Application.Reservations.MyReservations;
using PadelBooking.Application.Reservations.Reschedule;

namespace PadelBooking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly GetMyReservations _getMyReservations;
    private readonly CancelReservation _cancelReservation;
    private readonly RescheduleReservation _rescheduleReservation;
    private readonly GetReservationAvailability _getAvailability;

    public ReservationsController(GetMyReservations getMyReservations, CancelReservation cancelReservation,
        RescheduleReservation rescheduleReservation,
        GetReservationAvailability getAvailability)
    {
        _getMyReservations = getMyReservations;
        _cancelReservation = cancelReservation;
        _rescheduleReservation = rescheduleReservation;
        _getAvailability = getAvailability;
    }

    [HttpPost]
    public IActionResult CreateReservation(CreateReservationRequest request)
    {
        // Direct creation would bypass Checkout. Clients must use the payment endpoint.
        return StatusCode(StatusCodes.Status402PaymentRequired, new
        {
            message = "Za rezervaciju je potrebno test plaćanje. Koristite /api/payments/checkout."
        });
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyReservations()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await _getMyReservations.ExecuteAsync(
            userId, HttpContext.RequestAborted));
    }

    [HttpPut("{id}/reschedule")]
    public async Task<IActionResult> RescheduleReservation(
        int id, RescheduleReservationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _rescheduleReservation.ExecuteAsync(
            new RescheduleReservationCommand(id, userId,
                request.StartTime, request.EndTime, request.AcknowledgeNoRefund,
                request.ExpectedNewPrice, request.ExpectedTopUpAmount),
            cancellationToken: HttpContext.RequestAborted);

        return RescheduleResponse(result);
    }

    [HttpPost("{id}/reschedule/quote")]
    public async Task<IActionResult> QuoteReschedule(int id, RescheduleReservationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _rescheduleReservation.ExecuteAsync(
            new RescheduleReservationCommand(id, userId, request.StartTime, request.EndTime),
            previewOnly: true, cancellationToken: HttpContext.RequestAborted);
        return result.Status == RescheduleReservationStatus.Success
            ? Ok(result.Quote) : RescheduleResponse(result);
    }

    private IActionResult RescheduleResponse(RescheduleReservationResult result)
    {
        return result.Status switch
        {
            RescheduleReservationStatus.NotFound => NotFound(
                "Rezervacija nije pronađena."),
            RescheduleReservationStatus.NotActiveFuture => Conflict(
                "Samo aktivnu buduću rezervaciju je moguće promeniti."),
            RescheduleReservationStatus.CourtNotFound => NotFound(
                "Teren nije pronađen ili više nije aktivan."),
            RescheduleReservationStatus.SameSlot => Conflict(
                "Izaberi termin različit od trenutnog."),
            RescheduleReservationStatus.Occupied => Conflict(
                "Izabrani termin je već zauzet."),
            RescheduleReservationStatus.Blocked => Conflict(
                "Izabrani termin je blokiran zbog održavanja."),
            RescheduleReservationStatus.PendingTopUp => Conflict(
                "Promena termina već čeka potvrdu doplate."),
            RescheduleReservationStatus.ConfirmationRequired => BadRequest(new
            {
                message = "Potvrdite da razlika u ceni neće biti refundirana.",
                quote = result.Quote
            }),
            RescheduleReservationStatus.QuoteChanged => Conflict(new
            {
                message = "Cena ili stanje uplate su promenjeni. Proverite novi iznos i potvrdite ponovo.",
                quote = result.Quote
            }),
            RescheduleReservationStatus.ProviderUnavailable => StatusCode(503, new
            {
                message = "Plaćanje trenutno nije dostupno. Pokušajte ponovo."
            }),
            RescheduleReservationStatus.CheckoutRequired => Ok(new
            {
                message = "Doplata je potrebna za novi termin.",
                checkoutUrl = result.CheckoutUrl,
                quote = result.Quote
            }),
            RescheduleReservationStatus.LockTimeout => LockTimeout(),
            _ => Ok(new
            {
                message = "Termin rezervacije je uspešno promenjen.",
                reservation = result.Reservation,
                quote = result.Quote
            })
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelReservation(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _cancelReservation.ExecuteAsync(
            id, userId, HttpContext.RequestAborted);
        return result.Status switch
        {
            CancelReservationStatus.NotFound => NotFound("Rezervacija nije pronađena."),
            CancelReservationStatus.AlreadyCancelled => Conflict(
                "Rezervacija je već otkazana."),
            CancelReservationStatus.NotActive => Conflict(
                "Rezervacija još nije potvrđena plaćanjem."),
            CancelReservationStatus.Completed => Conflict(
                "Završenu rezervaciju nije moguće otkazati."),
            CancelReservationStatus.Started => Conflict(
                "Rezervaciju koja je već počela nije moguće otkazati."),
            CancelReservationStatus.PendingTopUp => Conflict(
                "Otkazivanje nije moguće dok se obrađuje doplata za promenu termina."),
            CancelReservationStatus.LockTimeout => LockTimeout(),
            _ => Ok(new { message = "Rezervacija uspešno otkazana." })
        };
    }

    [AllowAnonymous]
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableSlots(
        int courtId, DateTime date, int? reservationId = null)
    {
        int? currentUserId = TryGetUserId(out var userId) ? userId : null;
        var availability = await _getAvailability.ExecuteAsync(
            courtId, date, reservationId, currentUserId,
            HttpContext.RequestAborted);
        return availability is null
            ? NotFound("Teren nije pronađen.")
            : Ok(availability);
    }

    private bool TryGetUserId(out int userId) => int.TryParse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        out userId);

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
