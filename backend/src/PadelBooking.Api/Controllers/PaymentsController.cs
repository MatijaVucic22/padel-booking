using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Errors;
using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Application.Payments;

namespace PadelBooking.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(
    StartCheckout startCheckout,
    GetPaymentStatus getPaymentStatus,
    ProcessCheckoutEvent processCheckoutEvent,
    IPaymentGateway gateway,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [Authorize]
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout(CreateReservationRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await startCheckout.ExecuteAsync(userId, request.CourtId,
            request.StartTime, request.EndTime, HttpContext.RequestAborted);

        if (result.Status == StartCheckoutStatus.Success)
            logger.LogInformation("Checkout odgovor spreman. ReservationId={ReservationId}.", result.ReservationId);

        return result.Status switch
        {
            StartCheckoutStatus.Unauthorized => Unauthorized(),
            StartCheckoutStatus.CourtNotFound => this.ApiError(404, ApiErrorCodes.CourtInactive,
                "Teren nije pronađen ili više nije aktivan."),
            StartCheckoutStatus.Occupied => this.ApiError(409, ApiErrorCodes.SlotUnavailable,
                "Izabrani termin je već zauzet."),
            StartCheckoutStatus.Blocked => this.ApiError(409, ApiErrorCodes.SlotUnavailable,
                "Izabrani termin je blokiran zbog održavanja."),
            StartCheckoutStatus.CheckoutWindowClosed => this.ApiError(400, ApiErrorCodes.CheckoutTooClose,
                "Za online plaćanje izaberite termin koji počinje za najmanje 34 minuta."),
            StartCheckoutStatus.LockTimeout => LockTimeout(),
            StartCheckoutStatus.ProviderUnavailable => this.ApiError(503, ApiErrorCodes.ProviderUnavailable,
                "Plaćanje trenutno nije dostupno. Pokušajte ponovo."),
            _ => Ok(new { reservationId = result.ReservationId, checkoutUrl = result.Url })
        };
    }

    [Authorize]
    [HttpGet("session/{sessionId}/status")]
    public async Task<IActionResult> GetStatus(string sessionId)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var status = await getPaymentStatus.ExecuteAsync(
                sessionId, userId, HttpContext.RequestAborted);
            return status is null ? this.ApiError(404, ApiErrorCodes.NotFound, "Plaćanje nije pronađeno.") : Ok(status);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Provera Stripe sesije nije uspela. SessionId={SessionId}, ErrorType={ErrorType}.",
                sessionId, exception.GetType().Name);
            return this.ApiError(503, ApiErrorCodes.ProviderUnavailable,
                "Provera plaćanja trenutno nije dostupna. Pokušajte ponovo.");
        }
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        CheckoutEvent checkoutEvent;
        try
        {
            checkoutEvent = gateway.VerifyWebhook(rawBody, Request.Headers["Stripe-Signature"].ToString());
        }
        catch (InvalidPaymentWebhookException)
        {
            return this.ApiError(400, ApiErrorCodes.InvalidWebhook, "Stripe webhook potpis nije ispravan.");
        }

        logger.LogInformation("Stripe webhook primljen. EventType={EventType}, SessionId={SessionId}, PaymentStatus={PaymentStatus}.",
            checkoutEvent.Type, checkoutEvent.SessionId, checkoutEvent.PaymentStatus);

        try
        {
            await processCheckoutEvent.ExecuteAsync(checkoutEvent, HttpContext.RequestAborted);
            return Ok();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Obrada Stripe webhook-a nije uspela.");
            return this.ApiError(503, ApiErrorCodes.ProviderUnavailable,
                "Obrada plaćanja trenutno nije dostupna. Pokušajte ponovo.");
        }
    }

    private bool TryGetUserId(out int userId) => int.TryParse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

    private IActionResult LockTimeout()
    {
        Response.Headers.RetryAfter = "1";
        return this.ApiError(503, ApiErrorCodes.CourtLockTimeout,
            "Teren je trenutno zauzet obradom drugog zahteva. Pokušajte ponovo.");
    }
}
