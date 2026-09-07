using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PadelBooking.Api.Serialization
{
    public sealed class LocalWallClockDateTimeJsonConverter : JsonConverter<DateTime>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss";

        public override DateTime Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException(
                    "Vreme mora biti string u formatu yyyy-MM-ddTHH:mm:ss bez timezone suffix-a."
                );
            }

            var value = reader.GetString();

            if (!DateTime.TryParseExact(
                    value,
                    Format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                throw new JsonException(
                    "Vreme mora biti u formatu yyyy-MM-ddTHH:mm:ss bez Z ili UTC offset-a."
                );
            }

            return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTime value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
        }
    }
}
