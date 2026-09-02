namespace PadelBooking.Api.DTOs
{
    public class CreateReservationRequest
    {
        public int CourtId { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }
    }
}