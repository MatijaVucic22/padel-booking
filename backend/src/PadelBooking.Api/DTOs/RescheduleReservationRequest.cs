namespace PadelBooking.Api.DTOs
{
    public class RescheduleReservationRequest : ReservationTimeRequest
    {
        public bool AcknowledgeNoRefund { get; set; }
        public decimal? ExpectedNewPrice { get; set; }
        public decimal? ExpectedTopUpAmount { get; set; }
    }
}
