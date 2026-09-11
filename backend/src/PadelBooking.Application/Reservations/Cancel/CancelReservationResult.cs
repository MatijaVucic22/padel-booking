namespace PadelBooking.Application.Reservations.Cancel;

public enum CancelReservationStatus
{
    Success,
    NotFound,
    AlreadyCancelled,
    Completed,
    Started
}

public sealed record CancelReservationResult(CancelReservationStatus Status);
