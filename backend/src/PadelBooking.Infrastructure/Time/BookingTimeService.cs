using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Infrastructure.Time
{
    public class BookingTimeService : IBookingTimeService
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeZoneInfo _businessTimeZone;

        public BookingTimeService(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
            _businessTimeZone = GetBusinessTimeZone();
        }

        public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(
            _timeProvider.GetUtcNow().UtcDateTime,
            _businessTimeZone
        );

        public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        private static TimeZoneInfo GetBusinessTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById(
                    "Central European Standard Time"
                );
            }
        }
    }
}
