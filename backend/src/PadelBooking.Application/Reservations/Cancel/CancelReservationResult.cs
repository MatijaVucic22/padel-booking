namespace PadelBooking.Application.Reservations.Cancel;

public enum CancelReservationStatus
{
    Success,
    NotFound,
    AlreadyCancelled,
    NotActive,
    Completed,
    Started,
    PendingTopUp,
    NoRefundAcknowledgementRequired,
    LockTimeout
}

public sealed record CancelReservationResult(CancelReservationStatus Status);
