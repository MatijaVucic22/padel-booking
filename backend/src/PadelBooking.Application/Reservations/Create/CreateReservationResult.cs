namespace PadelBooking.Application.Reservations.Create;

public enum CreateReservationStatus
{
    Success,
    Unauthorized,
    CourtNotFound,
    Occupied,
    Blocked,
    LockTimeout
}

public sealed record CreatedReservation(
    int Id,
    int CourtId,
    string CourtName,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalPrice,
    string Status);

public sealed record CreateReservationResult(
    CreateReservationStatus Status,
    CreatedReservation? Reservation = null);
