namespace PadelBooking.Application.Notifications;

public sealed record ReservationConfirmationEmail(
    string RecipientEmail,
    string UserFirstName,
    string CourtName,
    string CourtLocation,
    DateTime StartTime,
    DateTime EndTime,
    decimal TotalPrice,
    int ReservationId
);
