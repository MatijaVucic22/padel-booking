using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
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
    private static readonly DateTime FixedNow = new(2026, 9, 28, 17, 30, 0);

    [Fact]
    public async Task AdminCanCheckInValidReservationWithoutChangingBookingOrPayment()
    {
        var (admin, _) = await AuthenticatedClientAsync("Admin");
        var (_, userId) = await AuthenticatedClientAsync("User");
        using (admin)
        {
            var reservationId = await SeedReservationAsync(userId, "Active",
                FixedNow.AddMinutes(30), FixedNow.AddMinutes(90), withPayment: true);

            using var response = await PostCheckInAtAsync(admin, reservationId, FixedNow);

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
            Assert.Equal(FixedNow.AddMinutes(30), reservation.StartTime);
            Assert.Equal(FixedNow.AddMinutes(90), reservation.EndTime);
            Assert.Equal(2500m, reservation.TotalPrice);
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
            var reservationId = await SeedReservationAsync(userId, "Active",
                FixedNow.AddMinutes(30), FixedNow.AddMinutes(90));
            var first = await CheckInAtAsync(admin, reservationId, FixedNow);
            var second = await CheckInAtAsync(admin, reservationId, FixedNow);

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
            var reservationId = await SeedReservationAsync(userId, "Active",
                FixedNow.AddMinutes(30), FixedNow.AddMinutes(90));
            var results = await Task.WhenAll(
                CheckInAtAsync(firstAdmin, reservationId, FixedNow),
                CheckInAtAsync(secondAdmin, reservationId, FixedNow));

            Assert.Equal(results[0], results[1]);
            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(results[0], (await db.Reservations.AsNoTracking()
                .SingleAsync(item => item.Id == reservationId)).CheckedInAtUtc);
        }
    }

    [Theory]
    [InlineData("2026-09-28T16:59:00", HttpStatusCode.Conflict, "CHECK_IN_TOO_EARLY")]
    [InlineData("2026-09-28T16:59:59", HttpStatusCode.Conflict, "CHECK_IN_TOO_EARLY")]
    [InlineData("2026-09-28T17:00:00", HttpStatusCode.OK, null)]
    [InlineData("2026-09-28T17:01:00", HttpStatusCode.OK, null)]
    [InlineData("2026-09-28T18:00:00", HttpStatusCode.OK, null)]
    [InlineData("2026-09-28T19:00:00", HttpStatusCode.OK, null)]
    [InlineData("2026-09-28T19:59:59", HttpStatusCode.OK, null)]
    [InlineData("2026-09-28T20:00:00", HttpStatusCode.Conflict, "CHECK_IN_WINDOW_CLOSED")]
    [InlineData("2026-09-28T20:01:00", HttpStatusCode.Conflict, "CHECK_IN_WINDOW_CLOSED")]
    public async Task CheckInUsesStrictTimeWindow(
        string currentTime,
        HttpStatusCode expectedStatus,
        string? expectedCode)
    {
        var (admin, _) = await AuthenticatedClientAsync("Admin");
        var (_, userId) = await AuthenticatedClientAsync("User");
        using (admin)
        {
            var reservationId = await SeedReservationAsync(userId, "Active",
                new DateTime(2026, 9, 28, 18, 0, 0),
                new DateTime(2026, 9, 28, 20, 0, 0));

            using var response = await PostCheckInAtAsync(
                admin, reservationId, DateTime.Parse(currentTime));

            Assert.Equal(expectedStatus, response.StatusCode);
            if (expectedCode is not null)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(expectedCode, json.RootElement.GetProperty("code").GetString());
            }
        }
    }

    [Fact]
    public async Task CheckInUsesCurrentRescheduledTimes()
    {
        var (admin, _) = await AuthenticatedClientAsync("Admin");
        var (_, userId) = await AuthenticatedClientAsync("User");
        using (admin)
        {
            var reservationId = await SeedReservationAsync(userId, "Active",
                FixedNow.AddDays(2), FixedNow.AddDays(2).AddHours(1));
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var reservation = await db.Reservations.SingleAsync(item => item.Id == reservationId);
                reservation.StartTime = FixedNow.AddMinutes(30);
                reservation.EndTime = FixedNow.AddMinutes(90);
                await db.SaveChangesAsync();
            }

            using var response = await PostCheckInAtAsync(admin, reservationId, FixedNow);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private async Task<int> SeedReservationAsync(
        int userId,
        string status,
        DateTime startTime,
        DateTime endTime,
        bool withPayment = false)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
            StartTime = startTime,
            EndTime = endTime,
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

    private async Task<HttpResponseMessage> PostCheckInAtAsync(
        HttpClient authenticatedClient,
        int reservationId,
        DateTime localNow)
    {
        using var factory = host.CreateFactoryWithBookingTime(
            new TestBookingTimeService(localNow));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization =
            authenticatedClient.DefaultRequestHeaders.Authorization;
        return await client.PostAsJsonAsync(
            $"/api/admin/reservations/{reservationId}/check-in",
            new { verificationToken = TokenFor(reservationId) });
    }

    private async Task<DateTime> CheckInAtAsync(
        HttpClient client,
        int reservationId,
        DateTime localNow)
    {
        using var response = await PostCheckInAtAsync(client, reservationId, localNow);
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
