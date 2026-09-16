using Microsoft.AspNetCore.Http;

namespace PadelBooking.Api.Authentication;

public static class BrowserAuthCookie
{
    public const string Name = "PadelBooking.Auth";

    public static CookieOptions Options(IHostEnvironment environment, DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires
    };
}
