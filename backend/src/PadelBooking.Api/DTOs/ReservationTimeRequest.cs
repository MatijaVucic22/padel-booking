using PadelBooking.Api.Serialization;
using System.Text.Json.Serialization;

namespace PadelBooking.Api.DTOs
{
    public abstract class ReservationTimeRequest
    {
        [JsonConverter(typeof(LocalWallClockDateTimeJsonConverter))]
        public DateTime StartTime { get; set; }

        [JsonConverter(typeof(LocalWallClockDateTimeJsonConverter))]
        public DateTime EndTime { get; set; }
    }
}
