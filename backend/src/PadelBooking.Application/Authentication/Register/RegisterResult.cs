namespace PadelBooking.Application.Authentication.Register;

public sealed record RegisterResult(bool IsDuplicateEmail, AuthUser? User);
