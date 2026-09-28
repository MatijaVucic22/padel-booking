using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Security;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Reservations.Verification;

public static class ReservationVerificationStatuses
{
    public const string Valid = "Valid";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string Invalid = "Invalid";
}

public sealed record ReservationVerificationResult(
    int ReservationNumber,
    string CourtName,
    string Location,
    DateTime StartTime,
    DateTime EndTime,
    string VerificationStatus);

public sealed class VerifyReservation(
    IReservationRepository reservations,
    IReservationVerificationTokenService tokens,
    IBookingTimeService bookingTime)
{
    public async Task<ReservationVerificationResult?> ExecuteAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!tokens.TryValidateToken(token, out var reservationId)) return null;

        var reservation = await reservations.GetForVerificationAsync(
            reservationId, cancellationToken);
        if (reservation is null) return null;

        var status = reservation.Status switch
        {
            "Cancelled" => ReservationVerificationStatuses.Cancelled,
            _ when reservation.EndTime <= bookingTime.Now => ReservationVerificationStatuses.Expired,
            "Active" => ReservationVerificationStatuses.Valid,
            _ => ReservationVerificationStatuses.Invalid
        };

        return new ReservationVerificationResult(
            reservation.Id,
            reservation.Court.Name,
            reservation.Court.Location,
            reservation.StartTime,
            reservation.EndTime,
            status);
    }
}
