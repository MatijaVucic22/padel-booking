namespace PadelBooking.Application.Reservations.Availability;

public static class BookingCutoffPolicy
{
    public static readonly TimeSpan MinimumLeadTime = TimeSpan.FromMinutes(15);

    public static bool CanBook(DateTime slotStart, DateTime now) =>
        slotStart >= now + MinimumLeadTime;
}
