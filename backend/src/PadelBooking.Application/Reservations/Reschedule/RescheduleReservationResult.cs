namespace PadelBooking.Application.Reservations.Reschedule;

public enum RescheduleReservationStatus
{
    Success,
    NotFound,
    NotActiveFuture,
    CourtNotFound,
    SameSlot,
    Occupied,
    Blocked,
    PendingTopUp,
    ConfirmationRequired,
    QuoteChanged,
    CheckoutRequired,
    CheckoutWindowClosed,
    ProviderUnavailable,
    LockTimeout
}

public sealed record RescheduleQuote(
    decimal CurrentPrice,
    decimal NewPrice,
    decimal PaidCredit,
    decimal TopUpAmount,
    decimal NonRefundedDifference,
    bool RequiresNoRefundConfirmation);

public sealed record RescheduledReservation(
    int Id,
    int CourtId,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalPrice,
    string Status);

public sealed record RescheduleReservationResult(
    RescheduleReservationStatus Status,
    RescheduledReservation? Reservation = null,
    RescheduleQuote? Quote = null,
    string? CheckoutUrl = null);
