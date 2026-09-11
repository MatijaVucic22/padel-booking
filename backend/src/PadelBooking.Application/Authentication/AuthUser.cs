using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Authentication;

public sealed record AuthUser(int Id, string FirstName, string LastName, string Email, string Role)
{
    public static AuthUser FromUser(User user) => new(
        user.Id, user.FirstName, user.LastName, user.Email, user.Role);
}
