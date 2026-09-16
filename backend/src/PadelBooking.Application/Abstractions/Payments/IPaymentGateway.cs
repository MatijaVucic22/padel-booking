namespace PadelBooking.Application.Abstractions.Payments;

public sealed record CheckoutRequest(
    string CheckoutReference,
    string CourtName,
    string CustomerEmail,
    long AmountMinor,
    string Currency);

public sealed record CheckoutSession(
    string Id,
    string Url,
    DateTime ExpiresAtUtc);

public sealed record CheckoutEvent(
    string Type,
    string SessionId,
    string? PaymentIntentId,
    string? PaymentStatus,
    long? AmountTotalMinor,
    string? Currency,
    string? CustomerEmail,
    string? ClientReferenceId,
    string? MetadataReference);

public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<CheckoutEvent> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task ExpireSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    CheckoutEvent VerifyWebhook(string rawBody, string signature);
}

public sealed class InvalidPaymentWebhookException : Exception
{
    public InvalidPaymentWebhookException() : base("Stripe webhook potpis nije ispravan.") { }
}
