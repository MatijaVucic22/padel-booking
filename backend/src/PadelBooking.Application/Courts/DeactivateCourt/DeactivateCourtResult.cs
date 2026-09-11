namespace PadelBooking.Application.Courts.DeactivateCourt;

public enum DeactivateCourtStatus
{
    Success,
    NotFound,
    HasFutureReservations,
    LockTimeout
}

public sealed record DeactivateCourtResult(DeactivateCourtStatus Status);
