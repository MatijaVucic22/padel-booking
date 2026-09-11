using PadelBooking.Application.Abstractions.Persistence;

namespace PadelBooking.Application.Authentication.GetCurrentUser;

public sealed class GetCurrentUser
{
    private readonly IUserRepository _users;

    public GetCurrentUser(IUserRepository users)
    {
        _users = users;
    }

    public async Task<AuthUser?> ExecuteAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return user == null ? null : AuthUser.FromUser(user);
    }
}
