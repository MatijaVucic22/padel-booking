using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PadelBooking.Application.Abstractions.Authentication;
using PadelBooking.Application.Abstractions.Security;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Domain.Entities;
using PadelBooking.Infrastructure.Persistence;
using Xunit;

namespace PadelBooking.IntegrationTests;

[Collection("mysql-integration")]
public sealed class ReservationCheckInIntegrationTests(IntegrationTestHost host)
{
    [Fact]
    public async Task AdminCanCheckInValidReservationWithoutChangingBookingOrPayment()
    {
        var (admin, _) = await AuthenticatedClientAsync("Admin");
        var (_, userId) = await AuthenticatedClientAsync("User");
        using (admin)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true,
                withPayment: true);

            using var response = await admin.PostAsync(
                $"/api/admin/reservations/{reservationId}/check-in",
                JsonContent.Create(new { verificationToken = TokenFor(reservationId) }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("checkedIn").GetBoolean());
            var checkedInAt = json.RootElement.GetProperty("checkedInAtUtc").GetDateTime();

            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reservation = await db.Reservations.AsNoTracking()
                .SingleAsync(item => item.Id == reservationId);
            var payment = await db.Payments.AsNoTracking()
                .SingleAsync(item => item.ReservationId == reservationId);
            Assert.Equal(checkedInAt, reservation.CheckedInAtUtc);
            Assert.Equal("Active", reservation.Status);
            Assert.Equal(reservation.StartTime.AddHours(1), reservation.EndTime);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.Equal(PaymentFulfillmentStatus.Applied, payment.FulfillmentStatus);

            var token = scope.ServiceProvider
                .GetRequiredService<IReservationVerificationTokenService>()
                .CreateToken(reservationId);
            using var verification = await host.Client.GetAsync(
                $"/api/reservations/verify/{token}");
            verification.EnsureSuccessStatusCode();
            using var verificationJson = JsonDocument.Parse(
                await verification.Content.ReadAsStringAsync());
            Assert.True(verificationJson.RootElement.GetProperty("checkedIn").GetBoolean());
        }
    }

    [Fact]
    public async Task NormalUserCannotCheckInReservation()
    {
        var (user, userId) = await AuthenticatedClientAsync("User");
        using (user)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            using var response = await user.PostAsync(
                $"/api/admin/reservations/{reservationId}/check-in",
                JsonContent.Create(new { verificationToken = TokenFor(reservationId) }));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task UnauthenticatedRequestCannotCheckInReservation()
    {
        var (_, userId) = await AuthenticatedClientAsync("User");
        var reservationId = await SeedReservationAsync(userId, "Active", future: true);

        using var response = await host.Client.PostAsync(
            $"/api/admin/reservations/{reservationId}/check-in",
            JsonContent.Create(new { verificationToken = TokenFor(reservationId) }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Cancelled", true)]
    [InlineData("Active", false)]
    public async Task IneligibleReservationCannotBeCheckedIn(string status, bool future)
    {
        var (admin, _) = await AuthenticatedClientAsync("Admin");
        var (_, userId) = await AuthenticatedClientAsync("User");
        using (admin)
        {
            var reservationId = await SeedReservationAsync(userId, status, future);
            using var response = await admin.PostAsync(
                $"/api/admin/reservations/{reservationId}/check-in",
                JsonContent.Create(new { verificationToken = TokenFor(reservationId) }));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null((await db.Reservations.AsNoTracking()
                .SingleAsync(item => item.Id == reservationId)).CheckedInAtUtc);
        }
    }

    [Fact]
    public async Task RepeatedCheckInReturnsOriginalTimestamp()
    {
        var (admin, _) = await AuthenticatedClientAsync("Admin");
        var (_, userId) = await AuthenticatedClientAsync("User");
        using (admin)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            var first = await CheckInAsync(admin, reservationId);
            var second = await CheckInAsync(admin, reservationId);

            Assert.Equal(first, second);
        }
    }

    [Fact]
    public async Task ForgedTokenCannotCheckInReservation()
    {
        var (admin, _) = await AuthenticatedClientAsync("Admin");
        var (_, userId) = await AuthenticatedClientAsync("User");
        using (admin)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            using var response = await admin.PostAsJsonAsync(
                $"/api/admin/reservations/{reservationId}/check-in",
                new { verificationToken = "v1.42.not-a-valid-signature" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task ConcurrentCheckInRequestsReturnOnePersistedTimestamp()
    {
        var (firstAdmin, _) = await AuthenticatedClientAsync("Admin");
        var (secondAdmin, _) = await AuthenticatedClientAsync("Admin");
        var (_, userId) = await AuthenticatedClientAsync("User");
        using (firstAdmin)
        using (secondAdmin)
        {
            var reservationId = await SeedReservationAsync(userId, "Active", future: true);
            var results = await Task.WhenAll(
                CheckInAsync(firstAdmin, reservationId),
                CheckInAsync(secondAdmin, reservationId));

            Assert.Equal(results[0], results[1]);
            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(results[0], (await db.Reservations.AsNoTracking()
                .SingleAsync(item => item.Id == reservationId)).CheckedInAtUtc);
        }
    }

    private async Task<(HttpClient Client, int UserId)> AuthenticatedClientAsync(string role)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            FirstName = role,
            LastName = "CheckIn",
            Email = $"check-in-{Guid.NewGuid():N}@example.test",
            PasswordHash = "integration-test-not-used",
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>()
            .GenerateAccessToken(user);
        return (host.ClientFactoryWithToken(token), user.Id);
    }

    private async Task<int> SeedReservationAsync(
        int userId,
        string status,
        bool future,
        bool withPayment = false)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IBookingTimeService>().Now;
        var start = future
            ? now.Date.AddDays(2).AddHours(18)
            : now.Date.AddDays(-2).AddHours(18);
        var reservation = new Reservation
        {
            UserId = userId,
            Court = new Court
            {
                Name = $"Check-in Court {Guid.NewGuid():N}",
                Location = "Beograd",
                PricePerHour = 2500m,
                IsActive = true
            },
            StartTime = start,
            EndTime = start.AddHours(1),
            TotalPrice = 2500m,
            Status = status
        };
        db.Reservations.Add(reservation);

        if (withPayment)
        {
            db.Payments.Add(new Payment
            {
                Reservation = reservation,
                Amount = 2500m,
                Status = PaymentStatus.Paid,
                FulfillmentStatus = PaymentFulfillmentStatus.Applied,
                Purpose = PaymentPurpose.InitialBooking,
                CreatedAtUtc = DateTime.UtcNow,
                SessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(35),
                ExternalSessionId = $"cs_check_in_{Guid.NewGuid():N}"
            });
        }

        await db.SaveChangesAsync();
        return reservation.Id;
    }

    private async Task<DateTime> CheckInAsync(HttpClient client, int reservationId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/admin/reservations/{reservationId}/check-in",
            new { verificationToken = TokenFor(reservationId) });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("checkedInAtUtc").GetDateTime();
    }

    private string TokenFor(int reservationId)
    {
        using var scope = host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IReservationVerificationTokenService>()
            .CreateToken(reservationId);
    }
}
