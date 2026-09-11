using Microsoft.AspNetCore.Identity;
using PadelBooking.Application.Abstractions.Authentication;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Infrastructure.Authentication;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly User DummyUser = new();
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly string DummyPasswordHash =
        Hasher.HashPassword(DummyUser, "DummyPassword1");

    public string HashPassword(User user, string password) =>
        Hasher.HashPassword(user, password);

    public bool VerifyPassword(User user, string passwordHash, string providedPassword) =>
        Hasher.VerifyHashedPassword(user, passwordHash, providedPassword) !=
            PasswordVerificationResult.Failed;

    public void PerformDummyVerification(string providedPassword) =>
        Hasher.VerifyHashedPassword(DummyUser, DummyPasswordHash, providedPassword);
}
