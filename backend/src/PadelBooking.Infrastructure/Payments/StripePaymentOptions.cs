namespace PadelBooking.Infrastructure.Payments;

public sealed class StripePaymentOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string FrontendBaseUrl { get; set; } = string.Empty;
}
