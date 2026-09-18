using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PadelBooking.Application.Abstractions.Authentication;
using PadelBooking.Infrastructure.Persistence;
using Xunit;

namespace PadelBooking.IntegrationTests;

[Collection("mysql-integration")]
public sealed class AuthCookieIntegrationTests(IntegrationTestHost host)
{
    [Fact]
    public async Task LoginCookie_AuthenticatesMe_AndLogoutExpiresIt()
    {
        var email = $"cookie-{Guid.NewGuid():N}@example.test";
        using var register = await host.Client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test", lastName = "Igrac", email, password = "IntegrationPass1"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        using var registerJson = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        Assert.False(registerJson.RootElement.TryGetProperty("token", out _));

        using var login = await host.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email, password = "IntegrationPass1"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.False(loginJson.RootElement.TryGetProperty("token", out _));
        Assert.Equal(email, loginJson.RootElement.GetProperty("user").GetProperty("email").GetString());
        var setCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("PadelBooking.Auth=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);

        var cookie = setCookie.Split(';')[0];
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Add("Cookie", cookie);
        using var me = await host.Client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        using var maliciousLogout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        maliciousLogout.Headers.Add("Cookie", cookie);
        maliciousLogout.Headers.Add("Origin", "https://untrusted.example.test");
        using var forbidden = await host.Client.SendAsync(maliciousLogout);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType?.MediaType);
        using var forbiddenJson = JsonDocument.Parse(await forbidden.Content.ReadAsStringAsync());
        Assert.Equal("FORBIDDEN", forbiddenJson.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(forbiddenJson.RootElement.GetProperty("traceId").GetString()));

        using var noOriginLogout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        noOriginLogout.Headers.Add("Cookie", cookie);
        using var missingOrigin = await host.Client.SendAsync(noOriginLogout);
        Assert.Equal(HttpStatusCode.Forbidden, missingOrigin.StatusCode);

        using var configuredOriginLogout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        configuredOriginLogout.Headers.Add("Cookie", cookie);
        configuredOriginLogout.Headers.Add("Origin", "https://configured-frontend.example.test");
        using var configuredOrigin = await host.Client.SendAsync(configuredOriginLogout);
        Assert.Equal(HttpStatusCode.OK, configuredOrigin.StatusCode);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", cookie);
        logoutRequest.Headers.Add("Origin", "http://localhost:5173");
        using var logout = await host.Client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Contains("PadelBooking.Auth=", Assert.Single(logout.Headers.GetValues("Set-Cookie")));

        using var afterLogout = await host.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task BearerStillWorks_AndInvalidOrExpiredCookiesDoNotAuthenticate()
    {
        var email = $"bearer-{Guid.NewGuid():N}@example.test";
        using var register = await host.Client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test", lastName = "Igrac", email, password = "IntegrationPass1"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
        var token = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>()
            .GenerateAccessToken(user);

        using var bearerClient = host.ClientFactoryWithToken(token);
        using var bearerMe = await bearerClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, bearerMe.StatusCode);

        using var bearerWithoutOrigin = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        bearerWithoutOrigin.Headers.Add("Cookie", "PadelBooking.Auth=invalid-token");
        using var bearerLogout = await bearerClient.SendAsync(bearerWithoutOrigin);
        Assert.Equal(HttpStatusCode.OK, bearerLogout.StatusCode);

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        invalidRequest.Headers.Add("Cookie", "PadelBooking.Auth=invalid-token");
        using var invalid = await host.Client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            "integration-tests-only-jwt-key-at-least-32-characters"));
        var expiredToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "PadelBooking.IntegrationTests",
            audience: "PadelBooking.IntegrationTests",
            claims: [new Claim(ClaimTypes.NameIdentifier, "1")],
            notBefore: DateTime.UtcNow.AddHours(-1),
            expires: DateTime.UtcNow.AddMinutes(-10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
        using var expiredRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        expiredRequest.Headers.Add("Cookie", $"PadelBooking.Auth={expiredToken}");
        using var expired = await host.Client.SendAsync(expiredRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

}
