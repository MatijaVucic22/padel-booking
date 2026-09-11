namespace PadelBooking.Application.Reservations.Availability;

public sealed record AvailableReservationSlot(
    DateTime StartTime,
    DateTime EndTime);

public sealed record ReservationAvailability(
    int CourtId,
    string CourtName,
    DateTime Date,
    IReadOnlyList<AvailableReservationSlot> Slots);
