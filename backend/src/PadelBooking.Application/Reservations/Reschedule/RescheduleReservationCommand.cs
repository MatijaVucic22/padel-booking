namespace PadelBooking.Application.Reservations.Reschedule;

public sealed record RescheduleReservationCommand(
    int Id,
    int UserId,
    DateTime StartTime,
    DateTime EndTime);
