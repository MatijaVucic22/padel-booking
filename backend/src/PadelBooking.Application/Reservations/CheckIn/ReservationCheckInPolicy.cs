namespace PadelBooking.Application.Reservations.CheckIn;

public enum ReservationCheckInWindowStatus
{
    TooEarly,
    Open,
    Closed
}

public static class ReservationCheckInPolicy
{
    public static readonly TimeSpan EarlyCheckInWindow = TimeSpan.FromMinutes(60);

    public static ReservationCheckInWindowStatus GetStatus(
        DateTime startTime,
        DateTime endTime,
        DateTime now)
    {
        if (now < GetAvailableFrom(startTime))
            return ReservationCheckInWindowStatus.TooEarly;

        return now < endTime
            ? ReservationCheckInWindowStatus.Open
            : ReservationCheckInWindowStatus.Closed;
    }

    public static DateTime GetAvailableFrom(DateTime startTime) =>
        startTime.Subtract(EarlyCheckInWindow);
}
