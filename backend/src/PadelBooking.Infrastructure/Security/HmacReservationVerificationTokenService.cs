using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PadelBooking.Application.Abstractions.Security;

namespace PadelBooking.Infrastructure.Security;

public sealed class HmacReservationVerificationTokenService(
    IOptions<QrVerificationOptions> options) : IReservationVerificationTokenService
{
    private const string Version = "v1";

    public string CreateToken(int reservationId)
    {
        if (reservationId <= 0) throw new ArgumentOutOfRangeException(nameof(reservationId));

        var payload = $"{Version}.{reservationId.ToString(CultureInfo.InvariantCulture)}";
        var signature = Sign(payload);
        return $"{payload}.{ToBase64Url(signature)}";
    }

    public bool TryValidateToken(string token, out int reservationId)
    {
        reservationId = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.');
        if (parts.Length != 3 || parts[0] != Version ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedId) ||
            parsedId <= 0 || parts[1] != parsedId.ToString(CultureInfo.InvariantCulture))
            return false;

        byte[] suppliedSignature;
        try
        {
            suppliedSignature = FromBase64Url(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = Sign($"{Version}.{parts[1]}");
        if (suppliedSignature.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            return false;

        reservationId = parsedId;
        return true;
    }

    private byte[] Sign(string payload)
    {
        var key = Encoding.UTF8.GetBytes(options.Value.Key);
        if (key.Length < 32)
            throw new InvalidOperationException("QR verification key is not configured securely.");

        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
    }

    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += (base64.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Invalid base64url value.")
        };
        return Convert.FromBase64String(base64);
    }
}
