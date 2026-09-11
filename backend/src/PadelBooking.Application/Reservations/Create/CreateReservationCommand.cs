namespace PadelBooking.Application.Reservations.Create;

public sealed record CreateReservationCommand(
    int UserId,
    int CourtId,
    DateTime StartTime,
    DateTime EndTime);
