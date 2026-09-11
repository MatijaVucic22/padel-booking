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
    LockTimeout
}

public sealed record RescheduledReservation(
    int Id,
    int CourtId,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalPrice,
    string Status);

public sealed record RescheduleReservationResult(
    RescheduleReservationStatus Status,
    RescheduledReservation? Reservation = null);
