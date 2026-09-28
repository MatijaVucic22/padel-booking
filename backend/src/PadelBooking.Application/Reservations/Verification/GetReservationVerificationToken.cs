using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Security;

namespace PadelBooking.Application.Reservations.Verification;

public sealed record ReservationVerificationToken(string Token, string VerificationPath);

public sealed class GetReservationVerificationToken(
    IReservationRepository reservations,
    IReservationVerificationTokenService tokens)
{
    public async Task<ReservationVerificationToken?> ExecuteAsync(
        int reservationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var courtId = await reservations.GetCourtIdForUserAsync(
            reservationId, userId, cancellationToken);
        if (courtId is null) return null;

        var token = tokens.CreateToken(reservationId);
        return new ReservationVerificationToken(
            token,
            $"/verify-booking/{token}");
    }
}
