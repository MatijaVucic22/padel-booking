namespace PadelBooking.Domain.Entities
{
    public class BlockedPeriod
    {
        public int Id { get; set; }
        public int CourtId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public Court Court { get; set; } = null!;
    }
}
