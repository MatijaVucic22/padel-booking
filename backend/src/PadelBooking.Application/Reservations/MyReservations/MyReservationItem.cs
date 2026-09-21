namespace PadelBooking.Application.Reservations.MyReservations;

public sealed record MyReservationItem(
    int Id,
    int CourtId,
    string CourtName,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalPrice,
    decimal PaidAmount,
    bool CanCancel,
    string Status);
