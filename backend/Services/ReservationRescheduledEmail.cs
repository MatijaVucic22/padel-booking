namespace PadelBooking.Api.Services;

public sealed record ReservationRescheduledEmail(
    string RecipientEmail,
    string UserFirstName,
    string CourtName,
    DateTime OldStartTime,
    DateTime OldEndTime,
    DateTime NewStartTime,
    DateTime NewEndTime,
    decimal NewTotalPrice,
    int ReservationId
);
