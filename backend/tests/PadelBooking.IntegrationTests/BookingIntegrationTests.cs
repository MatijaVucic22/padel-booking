using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
public sealed class BookingIntegrationTests(IntegrationTestHost host)
{
    private const string Password = "IntegrationPass1";

    [Fact]
    public async Task Register_Login_And_AccessProtectedEndpoint()
    {
        var email = UniqueEmail();
        using var register = await host.Client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test", lastName = "Igrac", email, password = Password
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var login = await host.Client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.False(loginJson.RootElement.TryGetProperty("token", out _));

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Email == email);
        var token = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>()
            .GenerateAccessToken(user);

        using var client = host.ClientFactoryWithToken(token);
        using var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meJson = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal(email, meJson.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        using var response = await host.Client.GetAsync("/api/reservations/my");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentCheckoutForSameCourtAndTime_AllowsOnlyOneHold()
    {
        var courtId = await SeedCourtAsync();
        var (start, end) = Slot(10);
        using var firstClient = await AuthenticatedClientAsync();
        using var secondClient = await AuthenticatedClientAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () => { await gate.Task; return await CheckoutAsync(firstClient, courtId, start, end); });
        var second = Task.Run(async () => { await gate.Task; return await CheckoutAsync(secondClient, courtId, start, end); });
        gate.SetResult();
        var responses = await Task.WhenAll(first, second);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var holds = await db.Reservations.AsNoTracking()
            .CountAsync(reservation => reservation.CourtId == courtId &&
                reservation.StartTime == start && reservation.EndTime == end &&
                reservation.Status == "PendingPayment");
        Assert.Equal(1, holds);
        Assert.Equal(1, await db.Payments.AsNoTracking()
            .CountAsync(payment => payment.Reservation.CourtId == courtId));
    }

    [Fact]
    public async Task BlockedPeriod_PreventsCheckout()
    {
        var courtId = await SeedCourtAsync();
        var (start, _) = Slot(10);
        using var admin = await AuthenticatedClientAsync(admin: true);
        using var user = await AuthenticatedClientAsync();
        using var blocked = await admin.PostAsJsonAsync("/api/admin/blocked-periods", new
        {
            courtId, startTime = Local(start), endTime = Local(start.AddHours(2)), reason = "Odrzavanje"
        });
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);

        using var checkout = await CheckoutAsync(user, courtId, start.AddHours(1), start.AddHours(2));
        Assert.Equal(HttpStatusCode.Conflict, checkout.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Reservations.AnyAsync(reservation => reservation.CourtId == courtId));
    }

    [Fact]
    public async Task SuccessfulPaymentCompletion_ConfirmsReservation()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (reservationId, sessionId) = await CreateCheckoutAsync(user, courtId, start, end);
        var pending = await SnapshotAsync(sessionId);
        Assert.Equal(PaymentStatus.Pending, pending.PaymentStatus);
        Assert.Equal("PendingPayment", pending.ReservationStatus);

        host.Gateway.SetPaid(sessionId);
        using var status = await user.GetAsync($"/api/payments/session/{sessionId}/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var statusJson = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.True(statusJson.RootElement.GetProperty("confirmed").GetBoolean());

        var paid = await SnapshotAsync(sessionId);
        Assert.Equal(reservationId, paid.ReservationId);
        Assert.Equal(PaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal("Active", paid.ReservationStatus);
        Assert.Equal($"pi_test_{sessionId}", paid.PaymentIntentId);
        Assert.Equal(1, host.Email.ConfirmationCount(reservationId));
    }

    [Fact]
    public async Task PaymentCompletion_IsIdempotent()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (reservationId, sessionId) = await CreateCheckoutAsync(user, courtId, start, end);
        host.Gateway.SetPaid(sessionId);

        using var first = await WebhookAsync(sessionId);
        using var second = await WebhookAsync(sessionId);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var status = await user.GetAsync($"/api/payments/session/{sessionId}/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Payments.CountAsync(payment => payment.ReservationId == reservationId));
        Assert.Equal(1, await db.Reservations.CountAsync(reservation => reservation.Id == reservationId));
        Assert.Equal("Active", (await db.Reservations.SingleAsync(reservation => reservation.Id == reservationId)).Status);
        Assert.Equal(1, host.Email.ConfirmationCount(reservationId));
    }

    [Fact]
    public async Task UnpaidCheckout_DoesNotConfirmReservation()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (reservationId, sessionId) = await CreateCheckoutAsync(user, courtId, start, end);

        using var status = await user.GetAsync($"/api/payments/session/{sessionId}/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var json = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("confirmed").GetBoolean());
        var state = await SnapshotAsync(sessionId);
        Assert.Equal(reservationId, state.ReservationId);
        Assert.Equal(PaymentStatus.Pending, state.PaymentStatus);
        Assert.Equal("PendingPayment", state.ReservationStatus);
        Assert.Equal(0, host.Email.ConfirmationCount(reservationId));
    }

    [Fact]
    public async Task UserCannotCancelAnotherUsersReservation()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        using var other = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (reservationId, sessionId) = await CreateCheckoutAsync(owner, courtId, start, end);
        host.Gateway.SetPaid(sessionId);
        using var confirmed = await owner.GetAsync($"/api/payments/session/{sessionId}/status");
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        using var cancel = await other.DeleteAsync($"/api/reservations/{reservationId}");
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal("Active", (await SnapshotAsync(sessionId)).ReservationStatus);
    }

    [Fact]
    public async Task RescheduleCannotOverlapExistingReservation()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (firstStart, firstEnd) = Slot(10);
        var secondStart = firstEnd;
        var secondEnd = secondStart.AddHours(1);
        var (firstId, firstSession) = await CreateCheckoutAsync(user, courtId, firstStart, firstEnd);
        var (_, secondSession) = await CreateCheckoutAsync(user, courtId, secondStart, secondEnd);
        host.Gateway.SetPaid(firstSession);
        host.Gateway.SetPaid(secondSession);
        using var firstStatus = await user.GetAsync($"/api/payments/session/{firstSession}/status");
        using var secondStatus = await user.GetAsync($"/api/payments/session/{secondSession}/status");
        Assert.Equal(HttpStatusCode.OK, firstStatus.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondStatus.StatusCode);

        using var reschedule = await user.PutAsJsonAsync($"/api/reservations/{firstId}/reschedule", new
        {
            startTime = Local(secondStart), endTime = Local(secondEnd)
        });
        Assert.Equal(HttpStatusCode.Conflict, reschedule.StatusCode);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await db.Reservations.AsNoTracking().SingleAsync(reservation => reservation.Id == firstId);
        Assert.Equal(firstStart, original.StartTime);
        Assert.Equal(firstEnd, original.EndTime);
    }

    [Fact]
    public async Task DirectReservationPost_CannotBypassPayment()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        using var response = await user.PostAsJsonAsync("/api/reservations", new
        {
            courtId, startTime = Local(start), endTime = Local(end)
        });
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Reservations.AnyAsync(reservation => reservation.CourtId == courtId));
    }

    private async Task<int> SeedCourtAsync()
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var court = new Court
        {
            Name = $"Integration {Guid.NewGuid():N}", Location = "Nis",
            PricePerHour = 2000m, IsActive = true
        };
        db.Courts.Add(court);
        await db.SaveChangesAsync();
        return court.Id;
    }

    private async Task<HttpClient> AuthenticatedClientAsync(bool admin = false)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            FirstName = "Test", LastName = "Igrac", Email = UniqueEmail(),
            PasswordHash = "integration-test-not-used", Role = admin ? "Admin" : "User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>().GenerateAccessToken(user);
        return host.ClientFactoryWithToken(token);
    }

    private static string UniqueEmail() => $"integration-{Guid.NewGuid():N}@example.test";

    private (DateTime Start, DateTime End) Slot(int hour)
    {
        using var scope = host.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IBookingTimeService>().Now;
        var start = now.Date.AddDays(7).AddHours(hour);
        return (start, start.AddHours(1));
    }

    private static string Local(DateTime value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> CheckoutAsync(HttpClient client, int courtId,
        DateTime start, DateTime end) => client.PostAsJsonAsync("/api/payments/checkout", new
        {
            courtId, startTime = Local(start), endTime = Local(end)
        });

    private async Task<(int ReservationId, string SessionId)> CreateCheckoutAsync(
        HttpClient client, int courtId, DateTime start, DateTime end)
    {
        using var response = await CheckoutAsync(client, courtId, start, end);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var reservationId = json.RootElement.GetProperty("reservationId").GetInt32();
        var url = json.RootElement.GetProperty("checkoutUrl").GetString()!;
        var sessionId = new Uri(url).Segments.Last().Trim('/');
        return (reservationId, sessionId);
    }

    private async Task<HttpResponseMessage> WebhookAsync(string sessionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(sessionId, Encoding.UTF8, "text/plain")
        };
        request.Headers.Add("Stripe-Signature", "integration-test");
        return await host.Client.SendAsync(request);
    }

    private async Task<PaymentSnapshot> SnapshotAsync(string sessionId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db.Payments.AsNoTracking().Include(item => item.Reservation)
            .SingleAsync(item => item.ExternalSessionId == sessionId);
        return new(payment.ReservationId, payment.Status, payment.Reservation.Status,
            payment.ExternalPaymentIntentId);
    }

    private sealed record PaymentSnapshot(int ReservationId, PaymentStatus PaymentStatus,
        string ReservationStatus, string? PaymentIntentId);
}
