using PadelBooking.Application.Abstractions.Authentication;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Authentication.Register;

public sealed class RegisterUser
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IBookingTimeService _bookingTime;

    public RegisterUser(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IBookingTimeService bookingTime)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _bookingTime = bookingTime;
    }

    public async Task<RegisterResult> ExecuteAsync(
        RegisterCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = command.Email.Trim().ToLower();

        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            return new RegisterResult(true, null);
        }

        var user = new User
        {
            FirstName = command.FirstName.Trim(),
            LastName = command.LastName.Trim(),
            Email = email,
            Role = "User",
            CreatedAt = _bookingTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, command.Password);
        _users.Add(user);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            return new RegisterResult(true, null);
        }

        return new RegisterResult(false, AuthUser.FromUser(user));
    }
}
