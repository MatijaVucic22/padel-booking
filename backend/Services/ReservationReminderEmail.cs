namespace PadelBooking.Api.Services;

public sealed record ReservationReminderEmail(
    string RecipientEmail,
    string UserFirstName,
    string CourtName,
    string CourtLocation,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalPrice,
    int ReservationId
);
