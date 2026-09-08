namespace PadelBooking.Api.DTOs
{
    public class CreateReservationRequest : ReservationTimeRequest
    {
        public int CourtId { get; set; }
    }
}
