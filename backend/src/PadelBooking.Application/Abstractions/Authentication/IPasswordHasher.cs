using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Abstractions.Authentication;

public interface IPasswordHasher
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string passwordHash, string providedPassword);
    void PerformDummyVerification(string providedPassword);
}
