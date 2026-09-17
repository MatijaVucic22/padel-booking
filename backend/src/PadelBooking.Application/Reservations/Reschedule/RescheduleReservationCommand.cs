namespace PadelBooking.Application.Reservations.Reschedule;

public sealed record RescheduleReservationCommand(
    int Id,
    int UserId,
    DateTime StartTime,
    DateTime EndTime,
    bool AcknowledgeNoRefund = false,
    decimal? ExpectedNewPrice = null,
    decimal? ExpectedTopUpAmount = null);
