namespace PadelBooking.Application.Courts.DeactivateCourt;

public enum DeactivateCourtStatus
{
    Success,
    NotFound,
    HasFutureReservations,
    HasPendingPayment,
    LockTimeout
}

public sealed record DeactivateCourtResult(DeactivateCourtStatus Status);
