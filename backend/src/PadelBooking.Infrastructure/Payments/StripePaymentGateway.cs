using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using PadelBooking.Application.Abstractions.Payments;
using Stripe;
using Stripe.Checkout;

namespace PadelBooking.Infrastructure.Payments;

public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly StripePaymentOptions _options;
    private readonly ILogger<StripePaymentGateway> _logger;
    private readonly Lazy<StripeClient> _client;

    public StripePaymentGateway(IOptions<StripePaymentOptions> options, ILogger<StripePaymentGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<StripeClient>(() =>
        {
            if (!_options.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal))
                throw new InvalidOperationException("Stripe test ključ nije konfigurisan.");
            return new StripeClient(_options.SecretKey);
        });
    }

    public async Task<CheckoutSession> CreateCheckoutAsync(
        CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var frontendUrl = GetFrontendUrl();
        var reference = request.CheckoutReference;
        var session = await Service().CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            PaymentMethodTypes = ["card"],
            CustomerEmail = request.CustomerEmail,
            ClientReferenceId = reference,
            ExpiresAt = DateTime.UtcNow.AddMinutes(35),
            SuccessUrl = $"{frontendUrl}/payment/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{frontendUrl}/payment/cancel",
            Metadata = new Dictionary<string, string> { ["checkoutReference"] = reference },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmount = request.AmountMinor,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"PadelBooking - {request.CourtName}"
                        }
                    }
                }
            ]
        }, new RequestOptions { IdempotencyKey = $"checkout-{reference}" }, cancellationToken);

        try
        {
            using var diagnosticTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var account = await new AccountService(_client.Value).GetSelfAsync(cancellationToken: diagnosticTimeout.Token);
            _logger.LogInformation("Stripe Checkout kreiran. AccountId={AccountId}, SessionId={SessionId}, PaymentStatus={PaymentStatus}.",
                account.Id, session.Id, session.PaymentStatus);
        }
        catch (Exception exception)
        {
            _logger.LogInformation("Stripe Checkout kreiran. SessionId={SessionId}, PaymentStatus={PaymentStatus}; AccountId nije dostupan ({ErrorType}).",
                session.Id, session.PaymentStatus, exception.GetType().Name);
        }

        return new CheckoutSession(session.Id, session.Url ?? string.Empty, session.ExpiresAt);
    }

    public async Task<CheckoutEvent> GetSessionAsync(
        string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await Service().GetAsync(sessionId, cancellationToken: cancellationToken);
        _logger.LogDebug("Stripe Checkout pročitan. SessionId={SessionId}, PaymentStatus={PaymentStatus}, SessionStatus={SessionStatus}.",
            session.Id, session.PaymentStatus, session.Status);
        var type = session.Status == "expired" ? "checkout.session.expired" :
            session.PaymentStatus == "paid" ? "checkout.session.completed" : "checkout.session.open";
        return ToEvent(type, session);
    }

    public async Task ExpireSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
        await Service().ExpireAsync(sessionId, cancellationToken: cancellationToken);

    public CheckoutEvent VerifyWebhook(string rawBody, string signature)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret) ||
            !_options.WebhookSecret.StartsWith("whsec_", StringComparison.Ordinal))
            throw new InvalidOperationException("STRIPE_WEBHOOK_SECRET nije konfigurisan.");

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(rawBody, signature, _options.WebhookSecret);
            if (stripeEvent.Data.Object is not Session session)
                return new CheckoutEvent(stripeEvent.Type, string.Empty, null, null, null, null, null, null, null);
            return ToEvent(stripeEvent.Type, session);
        }
        catch (Exception exception) when (exception is StripeException or ArgumentException or JsonException)
        {
            throw new InvalidPaymentWebhookException();
        }
    }

    private SessionService Service() => new(_client.Value);

    private string GetFrontendUrl()
    {
        if (!Uri.TryCreate(_options.FrontendBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("STRIPE_FRONTEND_URL nije konfigurisan.");
        return _options.FrontendBaseUrl.TrimEnd('/');
    }

    private static CheckoutEvent ToEvent(string type, Session session)
    {
        string? metadataReference = null;
        if (session.Metadata is not null)
            session.Metadata.TryGetValue("checkoutReference", out metadataReference);
        return new CheckoutEvent(type, session.Id, session.PaymentIntentId,
            session.PaymentStatus, session.AmountTotal, session.Currency,
            session.CustomerEmail ?? session.CustomerDetails?.Email,
            session.ClientReferenceId, metadataReference);
    }
}
