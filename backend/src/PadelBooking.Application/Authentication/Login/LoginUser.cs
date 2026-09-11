using PadelBooking.Application.Abstractions.Authentication;
using PadelBooking.Application.Abstractions.Persistence;

namespace PadelBooking.Application.Authentication.Login;

public sealed class LoginUser
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenGenerator _tokenGenerator;

    public LoginUser(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IAccessTokenGenerator tokenGenerator)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<LoginResult> ExecuteAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = command.Email.Trim().ToLower();
        var user = await _users.GetByNormalizedEmailAsync(email, cancellationToken);

        if (user == null)
        {
            _passwordHasher.PerformDummyVerification(command.Password);
            return new LoginResult(false, null, null);
        }

        if (!_passwordHasher.VerifyPassword(user, user.PasswordHash, command.Password))
        {
            return new LoginResult(false, null, null);
        }

        return new LoginResult(
            true,
            _tokenGenerator.GenerateAccessToken(user),
            AuthUser.FromUser(user));
    }
}
