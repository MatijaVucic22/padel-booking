namespace PadelBooking.Api.DTOs
{
    public class AvailableCourtsRequest
    {
        public DateTime StartTime { get; set; }
        public int DurationHours { get; set; }
    }
}
