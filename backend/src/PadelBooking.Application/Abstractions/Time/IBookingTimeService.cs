namespace PadelBooking.Application.Abstractions.Time;

public interface IBookingTimeService
{
    DateTime Now { get; }

    DateTime UtcNow { get; }
}
