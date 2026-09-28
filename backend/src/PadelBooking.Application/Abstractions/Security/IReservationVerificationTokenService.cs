namespace PadelBooking.Application.Abstractions.Security;

public interface IReservationVerificationTokenService
{
    string CreateToken(int reservationId);

    bool TryValidateToken(string token, out int reservationId);
}
