namespace PadelBooking.Api.DTOs
{
    public class CreateCourtRequest
    {
        public string Name { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal PricePerHour { get; set; }
    }
}