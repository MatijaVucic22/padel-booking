using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PadelBooking.Api.DTOs;
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
            StartCheckoutStatus.CourtNotFound => NotFound("Teren nije pronađen ili više nije aktivan."),
            StartCheckoutStatus.Occupied => Conflict("Izabrani termin je već zauzet."),
            StartCheckoutStatus.Blocked => Conflict("Izabrani termin je blokiran zbog održavanja."),
            StartCheckoutStatus.LockTimeout => LockTimeout(),
            StartCheckoutStatus.ProviderUnavailable => StatusCode(503, new
            {
                message = "Plaćanje trenutno nije dostupno. Pokušajte ponovo."
            }),
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
            return status is null ? NotFound("Plaćanje nije pronađeno.") : Ok(status);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Provera Stripe sesije nije uspela. SessionId={SessionId}, ErrorType={ErrorType}.",
                sessionId, exception.GetType().Name);
            return StatusCode(503, new { message = "Provera plaćanja trenutno nije dostupna. Pokušajte ponovo." });
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
            return BadRequest(new { message = "Stripe webhook potpis nije ispravan." });
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
            return StatusCode(503);
        }
    }

    private bool TryGetUserId(out int userId) => int.TryParse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);

    private IActionResult LockTimeout()
    {
        Response.Headers.RetryAfter = "1";
        return StatusCode(503, new
        {
            code = "COURT_LOCK_TIMEOUT",
            message = "Teren je trenutno zauzet obradom drugog zahteva. Pokušajte ponovo."
        });
    }
}
