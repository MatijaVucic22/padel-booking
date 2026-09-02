namespace PadelBooking.Api.Models
{
    public class Court
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal PricePerHour { get; set; }

        public bool IsActive { get; set; } = true;
    }
}