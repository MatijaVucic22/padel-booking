using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Errors;
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
        return this.ApiError(402, ApiErrorCodes.PaymentRequired,
            "Za rezervaciju je potrebno test plaćanje. Koristite /api/payments/checkout.");
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
            RescheduleReservationStatus.NotFound => this.ApiError(404, ApiErrorCodes.NotFound,
                "Rezervacija nije pronađena."),
            RescheduleReservationStatus.NotActiveFuture => this.ApiError(409, ApiErrorCodes.RescheduleNotAllowed,
                "Samo aktivnu buduću rezervaciju je moguće promeniti."),
            RescheduleReservationStatus.CourtNotFound => this.ApiError(404, ApiErrorCodes.CourtInactive,
                "Teren nije pronađen ili više nije aktivan."),
            RescheduleReservationStatus.SameSlot => this.ApiError(409, ApiErrorCodes.RescheduleNotAllowed,
                "Izaberi termin različit od trenutnog."),
            RescheduleReservationStatus.Occupied => this.ApiError(409, ApiErrorCodes.SlotUnavailable,
                "Izabrani termin je već zauzet."),
            RescheduleReservationStatus.Blocked => this.ApiError(409, ApiErrorCodes.SlotUnavailable,
                "Izabrani termin je blokiran zbog održavanja."),
            RescheduleReservationStatus.PendingTopUp => this.ApiError(409, ApiErrorCodes.PaymentPending,
                "Promena termina već čeka potvrdu doplate."),
            RescheduleReservationStatus.CheckoutWindowClosed => this.ApiError(400, ApiErrorCodes.CheckoutTooClose,
                "Novi termin je moguće izabrati najkasnije 15 minuta pre početka."),
            RescheduleReservationStatus.ConfirmationRequired => this.ApiError(400, ApiErrorCodes.RescheduleNotAllowed,
                "Potvrdite da razlika u ceni neće biti refundirana.", result.Quote),
            RescheduleReservationStatus.QuoteChanged => this.ApiError(409, ApiErrorCodes.Conflict,
                "Cena ili stanje uplate su promenjeni. Proverite novi iznos i potvrdite ponovo.", result.Quote),
            RescheduleReservationStatus.ProviderUnavailable => this.ApiError(503, ApiErrorCodes.ProviderUnavailable,
                "Plaćanje trenutno nije dostupno. Pokušajte ponovo."),
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
    public async Task<IActionResult> CancelReservation(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelReservationRequest? request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _cancelReservation.ExecuteAsync(
            id, userId, request?.AcknowledgeNoRefund == true,
            HttpContext.RequestAborted);
        return result.Status switch
        {
            CancelReservationStatus.NotFound => this.ApiError(404, ApiErrorCodes.NotFound, "Rezervacija nije pronađena."),
            CancelReservationStatus.AlreadyCancelled => this.ApiError(409, ApiErrorCodes.Conflict,
                "Rezervacija je već otkazana."),
            CancelReservationStatus.NotActive => this.ApiError(409, ApiErrorCodes.PaymentPending,
                "Rezervacija još nije potvrđena plaćanjem."),
            CancelReservationStatus.Completed => this.ApiError(409, ApiErrorCodes.CancellationNotAllowed,
                "Završenu rezervaciju nije moguće otkazati."),
            CancelReservationStatus.Started => this.ApiError(409, ApiErrorCodes.CancellationNotAllowed,
                "Rezervaciju koja je već počela nije moguće otkazati."),
            CancelReservationStatus.PendingTopUp => this.ApiError(409, ApiErrorCodes.PaymentPending,
                "Otkazivanje nije moguće dok se obrađuje doplata za promenu termina."),
            CancelReservationStatus.NoRefundAcknowledgementRequired => this.ApiError(400,
                ApiErrorCodes.CancellationNoRefundAcknowledgementRequired,
                "Potvrdite da razumete da se uplaćeni iznos ne vraća i ne pretvara u kredit."),
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
            ? this.ApiError(404, ApiErrorCodes.NotFound, "Teren nije pronađen.")
            : Ok(availability);
    }

    private bool TryGetUserId(out int userId) => int.TryParse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        out userId);

    private IActionResult LockTimeout()
    {
        Response.Headers.RetryAfter = "1";
        return this.ApiError(503, ApiErrorCodes.CourtLockTimeout,
            "Teren je trenutno zauzet obradom drugog zahteva. Pokušajte ponovo.");
    }
}
