namespace PadelBooking.Domain.Entities;

public sealed class Payment
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RSD";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string Provider { get; set; } = "Stripe";
    public string? ExternalSessionId { get; set; }
    public string? ExternalPaymentIntentId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime SessionExpiresAtUtc { get; set; }
    public Reservation Reservation { get; set; } = null!;
}
