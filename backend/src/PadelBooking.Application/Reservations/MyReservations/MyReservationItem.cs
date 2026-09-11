namespace PadelBooking.Application.Reservations.MyReservations;

public sealed record MyReservationItem(
    int Id,
    int CourtId,
    string CourtName,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalPrice,
    bool CanCancel,
    string Status);
