namespace PadelBooking.Application.Reservations.Availability;

public sealed record BookingTimeRange(DateTime StartTime, DateTime EndTime);

public static class BookingSlotCalculator
{
    public const int OpeningHour = 8;
    public const int ClosingHour = 22;

    public static IReadOnlyList<DateTime> GetAvailableStarts(
        DateTime firstDate,
        int dayCount,
        DateTime now,
        IEnumerable<BookingTimeRange> unavailableIntervals)
    {
        var intervals = unavailableIntervals.ToArray();
        var starts = new List<DateTime>();

        for (var day = 0; day < dayCount; day++)
        {
            var date = firstDate.Date.AddDays(day);
            for (var hour = OpeningHour; hour < ClosingHour; hour++)
            {
                var start = date.AddHours(hour);
                var end = start.AddHours(1);
                if (!BookingCutoffPolicy.CanBook(start, now)) continue;
                if (intervals.Any(interval =>
                    interval.StartTime < end && interval.EndTime > start)) continue;

                starts.Add(start);
            }
        }

        return starts;
    }
}
