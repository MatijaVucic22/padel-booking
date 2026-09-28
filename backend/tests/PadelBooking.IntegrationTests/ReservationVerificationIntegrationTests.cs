using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PadelBooking.Application.Abstractions.Authentication;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Domain.Entities;
using PadelBooking.Infrastructure.Persistence;
using Xunit;

namespace PadelBooking.IntegrationTests;

[Collection("mysql-integration")]
public sealed class ReservationVerificationIntegrationTests(IntegrationTestHost host)
{
    [Fact]
    public async Task OwnerCanGetVerificationToken()
    {
        var (owner, userId) = await AuthenticatedClientAsync();
        using (owner)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);

            using var response = await owner.GetAsync(
                $"/api/reservations/{reservationId}/verification-token");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var token = json.RootElement.GetProperty("token").GetString();
            Assert.StartsWith($"v1.{reservationId}.", token);
            Assert.Equal($"/verify-booking/{token}",
                json.RootElement.GetProperty("verificationPath").GetString());
        }
    }

    [Fact]
    public async Task OtherUserCannotGetVerificationToken()
    {
        var (owner, ownerId) = await AuthenticatedClientAsync();
        var (other, _) = await AuthenticatedClientAsync();
        using (owner)
        using (other)
        {
            var reservationId = await SeedReservationAsync(ownerId, "Active", future: true);

            using var response = await other.GetAsync(
                $"/api/reservations/{reservationId}/verification-token");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task ValidTokenVerifiesActiveFutureReservation()
    {
        var (owner, userId) = await AuthenticatedClientAsync();
        using (owner)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            var token = await GetTokenAsync(owner, reservationId);

            using var response = await host.Client.GetAsync(
                $"/api/reservations/verify/{token}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Valid", json.RootElement.GetProperty("verificationStatus").GetString());
            Assert.Equal(reservationId,
                json.RootElement.GetProperty("reservationNumber").GetInt32());
        }
    }

    [Fact]
    public async Task ForgedTokenIsRejectedSafely()
    {
        using var response = await host.Client.GetAsync(
            "/api/reservations/verify/v1.42.not-a-valid-signature");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_VERIFICATION_TOKEN",
            json.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("signature", await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangedReservationIdWithOldSignatureIsRejected()
    {
        var (owner, userId) = await AuthenticatedClientAsync();
        using (owner)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            var token = await GetTokenAsync(owner, reservationId);
            var parts = token.Split('.');
            var forged = $"{parts[0]}.{reservationId + 1}.{parts[2]}";

            using var response = await host.Client.GetAsync(
                $"/api/reservations/verify/{forged}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task CancelledReservationReturnsCancelled()
    {
        var (owner, userId) = await AuthenticatedClientAsync();
        using (owner)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            var token = await GetTokenAsync(owner, reservationId);

            await using (var scope = host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var reservation = await db.Reservations.SingleAsync(item => item.Id == reservationId);
                reservation.Status = "Cancelled";
                await db.SaveChangesAsync();
            }

            using var response = await host.Client.GetAsync(
                $"/api/reservations/verify/{token}");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Cancelled", json.RootElement.GetProperty("verificationStatus").GetString());
        }
    }

    [Fact]
    public async Task EndedReservationReturnsExpired()
    {
        var (owner, userId) = await AuthenticatedClientAsync();
        using (owner)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: false);
            var token = await GetTokenAsync(owner, reservationId);

            using var response = await host.Client.GetAsync(
                $"/api/reservations/verify/{token}");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Expired", json.RootElement.GetProperty("verificationStatus").GetString());
        }
    }

    [Fact]
    public async Task SameTokenReturnsCurrentRescheduledInterval()
    {
        var (owner, userId) = await AuthenticatedClientAsync();
        using (owner)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            var token = await GetTokenAsync(owner, reservationId);
            var newStart = BookingNow().Date.AddDays(5).AddHours(19);
            var newEnd = newStart.AddHours(2);

            await using (var scope = host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var reservation = await db.Reservations.SingleAsync(item => item.Id == reservationId);
                reservation.StartTime = newStart;
                reservation.EndTime = newEnd;
                await db.SaveChangesAsync();
            }

            using var response = await host.Client.GetAsync(
                $"/api/reservations/verify/{token}");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(newStart, json.RootElement.GetProperty("startTime").GetDateTime());
            Assert.Equal(newEnd, json.RootElement.GetProperty("endTime").GetDateTime());
        }
    }

    [Fact]
    public async Task VerificationEndpointIsPublic()
    {
        var (owner, userId) = await AuthenticatedClientAsync();
        using (owner)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            var token = await GetTokenAsync(owner, reservationId);

            using var response = await host.Client.GetAsync(
                $"/api/reservations/verify/{token}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task VerificationResponseContainsOnlyPublicReservationData()
    {
        var (owner, userId) = await AuthenticatedClientAsync();
        using (owner)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            var token = await GetTokenAsync(owner, reservationId);

            using var response = await host.Client.GetAsync(
                $"/api/reservations/verify/{token}");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var propertyNames = json.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Equal(6, propertyNames.Count);
            Assert.Subset(new HashSet<string>
            {
                "reservationNumber", "courtName", "location", "startTime", "endTime",
                "verificationStatus"
            }, propertyNames);
            Assert.DoesNotContain("email", propertyNames);
            Assert.DoesNotContain("userId", propertyNames);
            Assert.DoesNotContain("paymentStatus", propertyNames);
        }
    }

    private async Task<(HttpClient Client, int UserId)> AuthenticatedClientAsync()
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            FirstName = "QR",
            LastName = "Test",
            Email = $"qr-{Guid.NewGuid():N}@example.test",
            PasswordHash = "integration-test-not-used",
            Role = "User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>()
            .GenerateAccessToken(user);
        return (host.ClientFactoryWithToken(token), user.Id);
    }

    private async Task<int> SeedReservationAsync(int userId, string status, bool future)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = future
            ? BookingNow().Date.AddDays(3).AddHours(18)
            : BookingNow().Date.AddDays(-2).AddHours(18);
        var court = new Court
        {
            Name = $"QR Court {Guid.NewGuid():N}",
            Location = "Novi Sad",
            PricePerHour = 2500m,
            IsActive = true
        };
        var reservation = new Reservation
        {
            UserId = userId,
            Court = court,
            StartTime = start,
            EndTime = start.AddHours(1),
            TotalPrice = 2500m,
            Status = status
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }

    private DateTime BookingNow()
    {
        using var scope = host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IBookingTimeService>().Now;
    }

    private static async Task<string> GetTokenAsync(HttpClient owner, int reservationId)
    {
        using var response = await owner.GetAsync(
            $"/api/reservations/{reservationId}/verification-token");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }
}
