using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Abstractions.Authentication;

public interface IAccessTokenGenerator
{
    string GenerateAccessToken(User user);
}
