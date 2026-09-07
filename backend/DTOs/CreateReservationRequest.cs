using PadelBooking.Api.Serialization;
using System.Text.Json.Serialization;

namespace PadelBooking.Api.DTOs
{
    public class CreateReservationRequest
    {
        public int CourtId { get; set; }

        [JsonConverter(typeof(LocalWallClockDateTimeJsonConverter))]
        public DateTime StartTime { get; set; }

        [JsonConverter(typeof(LocalWallClockDateTimeJsonConverter))]
        public DateTime EndTime { get; set; }
    }
}
