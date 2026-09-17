namespace PadelBooking.Domain.Entities;

public sealed class Payment
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RSD";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public PaymentPurpose Purpose { get; set; } = PaymentPurpose.InitialBooking;
    public string Provider { get; set; } = "Stripe";
    public string? ExternalSessionId { get; set; }
    public string? ExternalPaymentIntentId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime SessionExpiresAtUtc { get; set; }
    public DateTime? TargetStartTime { get; set; }
    public DateTime? TargetEndTime { get; set; }
    public decimal? TargetTotalPrice { get; set; }
    public Reservation Reservation { get; set; } = null!;
}
