namespace PadelBooking.Application.Authentication.Login;

public sealed record LoginResult(bool Succeeded, string? Token, AuthUser? User);
