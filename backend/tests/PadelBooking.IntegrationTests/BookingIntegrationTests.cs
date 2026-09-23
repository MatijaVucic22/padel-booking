using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PadelBooking.Application.Abstractions.Authentication;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Payments;
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
        using var json = await AssertProblemAsync(response, "UNAUTHORIZED");
        Assert.Equal(401, json.RootElement.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    public async Task ProtectedEndpoint_WithRestrictiveAccept_ReturnsProblemDetails(string accept)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/reservations/my");
        request.Headers.Accept.ParseAdd(accept);
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var json = await AssertProblemAsync(response, "UNAUTHORIZED");
        Assert.Equal(401, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task AdminEndpoint_AsUser_ReturnsForbiddenProblemDetails()
    {
        using var user = await AuthenticatedClientAsync();
        using var response = await user.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var json = await AssertProblemAsync(response, "FORBIDDEN");
        Assert.Equal(403, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task AdminEndpoint_AsUser_WithHtmlAccept_ReturnsForbiddenProblemDetails()
    {
        using var user = await AuthenticatedClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await user.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var json = await AssertProblemAsync(response, "FORBIDDEN");
        Assert.Equal(403, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task AdminPaymentAttention_ReturnsPaidRequiresResolutionWithStoredReason()
    {
        var paymentId = await SeedAttentionPaymentAsync(
            PaymentStatus.Paid,
            PaymentFulfillmentStatus.RequiresResolution,
            DateTime.UtcNow.AddMinutes(-5),
            "TARGET_BLOCKED");
        using var admin = await AuthenticatedClientAsync(admin: true);

        using var response = await admin.GetAsync("/api/admin/payments/attention?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = json.RootElement.GetProperty("items").EnumerateArray()
            .Single(candidate => candidate.GetProperty("paymentId").GetInt32() == paymentId);

        Assert.Equal("Paid", item.GetProperty("paymentStatus").GetString());
        Assert.Equal("RequiresResolution", item.GetProperty("fulfillmentStatus").GetString());
        Assert.Equal("TARGET_BLOCKED", item.GetProperty("resolutionReasonCode").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(100, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.True(json.RootElement.GetProperty("totalCount").GetInt32() >= 1);
        Assert.True(json.RootElement.GetProperty("totalPages").GetInt32() >= 1);
    }

    [Fact]
    public async Task AdminPaymentAttention_MaxPageReturnsEmptyPageWithoutOverflow()
    {
        using var admin = await AuthenticatedClientAsync(admin: true);
        using var firstPageResponse = await admin.GetAsync(
            "/api/admin/payments/attention?page=1&pageSize=100");
        firstPageResponse.EnsureSuccessStatusCode();
        using var firstPage = JsonDocument.Parse(
            await firstPageResponse.Content.ReadAsStringAsync());
        var expectedTotalCount = firstPage.RootElement.GetProperty("totalCount").GetInt32();
        var expectedTotalPages = firstPage.RootElement.GetProperty("totalPages").GetInt32();

        using var response = await admin.GetAsync(
            "/api/admin/payments/attention?page=2147483647&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(int.MaxValue, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(100, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(expectedTotalCount, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(expectedTotalPages, json.RootElement.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task AdminPaymentAttention_DoesNotReturnPaidApplied()
    {
        var paymentId = await SeedAttentionPaymentAsync(
            PaymentStatus.Paid,
            PaymentFulfillmentStatus.Applied,
            DateTime.UtcNow.AddHours(-2));
        using var admin = await AuthenticatedClientAsync(admin: true);

        using var response = await admin.GetAsync("/api/admin/payments/attention?pageSize=100");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.DoesNotContain(json.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("paymentId").GetInt32() == paymentId);
    }

    [Fact]
    public async Task AdminPaymentAttention_DoesNotReturnRecentPending()
    {
        var paymentId = await SeedAttentionPaymentAsync(
            PaymentStatus.Pending,
            PaymentFulfillmentStatus.Pending,
            DateTime.UtcNow.AddMinutes(-30));
        using var admin = await AuthenticatedClientAsync(admin: true);

        using var response = await admin.GetAsync("/api/admin/payments/attention?pageSize=100");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.DoesNotContain(json.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("paymentId").GetInt32() == paymentId);
    }

    [Fact]
    public async Task AdminPaymentAttention_ReturnsPendingOlderThanThreshold()
    {
        var paymentId = await SeedAttentionPaymentAsync(
            PaymentStatus.Pending,
            PaymentFulfillmentStatus.Pending,
            DateTime.UtcNow.AddMinutes(-61));
        using var admin = await AuthenticatedClientAsync(admin: true);

        using var response = await admin.GetAsync("/api/admin/payments/attention?pageSize=100");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = json.RootElement.GetProperty("items").EnumerateArray()
            .Single(candidate => candidate.GetProperty("paymentId").GetInt32() == paymentId);

        Assert.Equal("Pending", item.GetProperty("paymentStatus").GetString());
        Assert.Equal("Pending", item.GetProperty("fulfillmentStatus").GetString());
    }

    [Fact]
    public async Task AdminPaymentAttention_AsUser_ReturnsForbidden()
    {
        using var user = await AuthenticatedClientAsync();

        using var response = await user.GetAsync("/api/admin/payments/attention");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = await AssertProblemAsync(response, "FORBIDDEN");
    }

    [Fact]
    public async Task ValidationFailure_ReturnsProblemDetailsWithFieldErrors()
    {
        using var response = await host.Client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "A", lastName = "Igrac", email = "invalid", password = Password
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await AssertProblemAsync(response, "VALIDATION_ERROR");
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("firstName", out _));
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task OccupiedCheckout_ReturnsConflictProblemDetails()
    {
        var courtId = await SeedCourtAsync();
        var (start, end) = Slot(10);
        using var user = await AuthenticatedClientAsync();
        using var first = await CheckoutAsync(user, courtId, start, end);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var second = await CheckoutAsync(user, courtId, start, end);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using var json = await AssertProblemAsync(second, "SLOT_UNAVAILABLE");
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task MissingCourt_ReturnsNotFoundProblemDetails()
    {
        using var response = await host.Client.GetAsync("/api/courts/2147483647");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await AssertProblemAsync(response, "NOT_FOUND");
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task CourtsList_WithoutLocationFilterReturnsNormalActiveList()
    {
        var courtId = await SeedCourtAsync(location: $"Novi Sad {Guid.NewGuid():N}");

        using var response = await host.Client.GetAsync("/api/courts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Contains(json.RootElement.EnumerateArray(),
            court => court.GetProperty("id").GetInt32() == courtId);
    }

    [Fact]
    public async Task CourtsList_LocationFilterIsCaseInsensitiveAndTrimsWhitespace()
    {
        var uniqueLocation = $"Beograd-{Guid.NewGuid():N}";
        var courtId = await SeedCourtAsync(location: $"{uniqueLocation}, Novi Beograd");
        var searches = new[]
        {
            uniqueLocation,
            uniqueLocation.ToLowerInvariant(),
            $"  {uniqueLocation}  "
        };

        foreach (var search in searches)
        {
            using var response = await host.Client.GetAsync(
                $"/api/courts?location={Uri.EscapeDataString(search)}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Contains(json.RootElement.EnumerateArray(),
                court => court.GetProperty("id").GetInt32() == courtId);
        }
    }

    [Fact]
    public async Task CourtsList_NonMatchingLocationReturnsEmptyList()
    {
        var missingLocation = $"Nepostojeca-{Guid.NewGuid():N}";

        using var response = await host.Client.GetAsync(
            $"/api/courts?location={Uri.EscapeDataString(missingLocation)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Empty(json.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task MissingApiRoute_WithHtmlAccept_ReturnsProblemDetails()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/does-not-exist");
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await AssertProblemAsync(response, "NOT_FOUND");
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task UnexpectedException_ReturnsSafeProblemDetails()
    {
        using var factory = host.CreateFactoryWithCourtFailure();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/courts");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var json = await AssertProblemAsync(response, "INTERNAL_ERROR");
        Assert.Equal("Došlo je do neočekivane greške. Pokušajte ponovo.",
            json.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("sensitive-db-password-marker", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnexpectedException_WithHtmlAccept_ReturnsSafeProblemDetails()
    {
        using var factory = host.CreateFactoryWithCourtFailure();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/courts");
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var json = await AssertProblemAsync(response, "INTERNAL_ERROR");
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("sensitive-db-password-marker", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LoginRateLimit_ReturnsProblemDetails()
    {
        using var factory = host.CreateIsolatedFactory();
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var rejectedLogin = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = UniqueEmail(), password = Password
            });
            Assert.Equal(HttpStatusCode.Unauthorized, rejectedLogin.StatusCode);
        }

        using var limited = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = UniqueEmail(), password = Password
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using var json = await AssertProblemAsync(limited, "RATE_LIMITED");
    }

    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal($"https://httpstatuses.com/{(int)response.StatusCode}",
            json.RootElement.GetProperty("type").GetString());
        return json;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellingPaidReservation_WithoutAcknowledgement_IsRejected(bool sendRequestBody)
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (reservationId, sessionId) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, sessionId);

        using var rejected = sendRequestBody
            ? await CancelAsync(owner, reservationId, acknowledgeNoRefund: false)
            : await owner.DeleteAsync($"/api/reservations/{reservationId}");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var problem = await AssertProblemAsync(
            rejected, "CANCELLATION_NO_REFUND_ACK_REQUIRED");
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Active", (await db.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == reservationId)).Status);
        var payment = await db.Payments.AsNoTracking()
            .SingleAsync(item => item.ReservationId == reservationId);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(PaymentFulfillmentStatus.Applied, payment.FulfillmentStatus);
    }

    [Fact]
    public async Task CancellingPaidReservation_WithAcknowledgement_PreservesPaymentAndRevenueWithoutReusableReservation()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        using var admin = await AuthenticatedClientAsync(admin: true);
        var (start, end) = Slot(10);
        var (reservationId, sessionId) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, sessionId);

        using var reservationsBefore = await owner.GetAsync("/api/reservations/my");
        reservationsBefore.EnsureSuccessStatusCode();
        using var reservationsJson = JsonDocument.Parse(await reservationsBefore.Content.ReadAsStringAsync());
        var paidReservation = reservationsJson.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == reservationId);
        var paidAmount = paidReservation.GetProperty("paidAmount").GetDecimal();
        Assert.True(paidAmount > 0);

        using var statsBefore = await admin.GetAsync("/api/admin/stats");
        statsBefore.EnsureSuccessStatusCode();
        using var beforeJson = JsonDocument.Parse(await statsBefore.Content.ReadAsStringAsync());
        var revenueBefore = beforeJson.RootElement.GetProperty("realizedRevenue").GetDecimal();

        using var cancelled = await CancelAsync(owner, reservationId, acknowledgeNoRefund: true);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await db.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == reservationId);
        var payments = await db.Payments.AsNoTracking()
            .Where(item => item.ReservationId == reservationId)
            .ToListAsync();
        Assert.Equal("Cancelled", reservation.Status);
        var payment = Assert.Single(payments);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(PaymentFulfillmentStatus.Applied, payment.FulfillmentStatus);
        Assert.Equal(paidAmount, payment.Amount);

        using var statsAfter = await admin.GetAsync("/api/admin/stats");
        statsAfter.EnsureSuccessStatusCode();
        using var afterJson = JsonDocument.Parse(await statsAfter.Content.ReadAsStringAsync());
        Assert.Equal(revenueBefore, afterJson.RootElement.GetProperty("realizedRevenue").GetDecimal());

        using var quoteAttempt = await owner.PostAsJsonAsync(
            $"/api/reservations/{reservationId}/reschedule/quote",
            new { startTime = Local(start.AddHours(2)), endTime = Local(end.AddHours(2)) });
        Assert.Equal(HttpStatusCode.Conflict, quoteAttempt.StatusCode);
    }

    [Fact]
    public async Task CancellingActiveReservationWithoutCapturedPayment_DoesNotRequireAcknowledgement()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        using var meResponse = await owner.GetAsync("/api/auth/me");
        meResponse.EnsureSuccessStatusCode();
        using var me = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        var userId = me.RootElement.GetProperty("id").GetInt32();
        var (start, end) = Slot(10);
        int reservationId;

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reservation = new Reservation
            {
                UserId = userId,
                CourtId = courtId,
                StartTime = start,
                EndTime = end,
                TotalPrice = 2000m,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();
            reservationId = reservation.Id;
        }

        using var cancelled = await owner.DeleteAsync($"/api/reservations/{reservationId}");
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        await using var verificationScope = host.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Cancelled", (await verificationDb.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == reservationId)).Status);
        Assert.False(await verificationDb.Payments.AnyAsync(
            item => item.ReservationId == reservationId));
    }

    [Fact]
    public async Task PaidCredit_IncludesOnlyPaidAppliedPayments()
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            FirstName = "Test", LastName = "Igrac", Email = UniqueEmail(),
            PasswordHash = "integration-test-not-used"
        };
        var court = new Court
        {
            Name = $"Integration {Guid.NewGuid():N}", Location = "Nis",
            PricePerHour = 2500m, IsActive = true
        };
        var (start, end) = Slot(10);
        var reservation = new Reservation
        {
            User = user, Court = court, StartTime = start, EndTime = end,
            TotalPrice = 2500m, Status = "Active", CreatedAt = DateTime.UtcNow
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        db.Payments.AddRange(
            PaymentForCredit(reservation.Id, 2500m, PaymentStatus.Paid,
                PaymentFulfillmentStatus.Applied),
            PaymentForCredit(reservation.Id, 1250m, PaymentStatus.Paid,
                PaymentFulfillmentStatus.RequiresResolution),
            PaymentForCredit(reservation.Id, 500m, PaymentStatus.Pending,
                PaymentFulfillmentStatus.Pending),
            PaymentForCredit(reservation.Id, 250m, PaymentStatus.Paid,
                PaymentFulfillmentStatus.NotApplicable));
        await db.SaveChangesAsync();

        var paidCredit = await scope.ServiceProvider.GetRequiredService<IPaymentRepository>()
            .GetPaidCreditAsync(reservation.Id);

        Assert.Equal(2500m, paidCredit);
    }

    [Fact]
    public async Task RequiresResolutionPayment_DoesNotEraseLaterRequiredTopUp()
    {
        var courtId = await SeedCourtAsync(2500m);
        using var owner = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (reservationId, sessionId) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, sessionId);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Courts.SingleAsync(court => court.Id == courtId)).PricePerHour = 1750m;
            db.Payments.Add(PaymentForCredit(reservationId, 1250m, PaymentStatus.Paid,
                PaymentFulfillmentStatus.RequiresResolution));
            await db.SaveChangesAsync();
        }

        var target = start.AddHours(2);
        var quote = await QuoteAsync(owner, reservationId, target, target.AddHours(2));

        Assert.Equal(3500m, quote.NewPrice);
        Assert.Equal(2500m, quote.PaidCredit);
        Assert.Equal(1000m, quote.TopUpAmount);
    }

    [Fact]
    public async Task LatePaidInitialBooking_RequiresResolutionAndReleasesProvisionalHold()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (reservationId, sessionId) = await CreateCheckoutAsync(owner, courtId, start, end);
        DateTime lateStart;
        DateTime lateEnd;

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = scope.ServiceProvider.GetRequiredService<IBookingTimeService>().Now;
            lateStart = now.AddMinutes(-5);
            lateEnd = now.AddMinutes(55);
            var reservation = await db.Reservations.SingleAsync(item => item.Id == reservationId);
            reservation.StartTime = lateStart;
            reservation.EndTime = lateEnd;
            await db.SaveChangesAsync();
        }

        host.Gateway.SetPaid(sessionId);
        using var status = await owner.GetAsync($"/api/payments/session/{sessionId}/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var statusJson = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
        Assert.False(statusJson.RootElement.GetProperty("confirmed").GetBoolean());
        Assert.Equal("RequiresResolution",
            statusJson.RootElement.GetProperty("fulfillmentStatus").GetString());

        await using var verification = host.Services.CreateAsyncScope();
        var verificationDb = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await verificationDb.Payments.AsNoTracking()
            .SingleAsync(item => item.ExternalSessionId == sessionId);
        var reservationState = await verificationDb.Reservations.AsNoTracking()
            .SingleAsync(item => item.Id == reservationId);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(PaymentFulfillmentStatus.RequiresResolution, payment.FulfillmentStatus);
        Assert.Equal("Cancelled", reservationState.Status);
        Assert.False(await verification.ServiceProvider.GetRequiredService<IReservationRepository>()
            .HasOverlapAsync(courtId, lateStart, lateEnd));
    }

    [Fact]
    public async Task ImpossiblePaidTopUp_RequiresResolutionAndDoesNotIncreasePaidCredit()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (reservationId, sessionId) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, sessionId);
        var target = start.AddHours(2);
        using var checkout = await StartTopUpAsync(owner, reservationId, target);
        var topUpSessionId = SessionIdFromResponse(await checkout.Content.ReadAsStringAsync());

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.BlockedPeriods.Add(new BlockedPeriod
            {
                CourtId = courtId,
                StartTime = target,
                EndTime = target.AddHours(2),
                Reason = "Integration conflict",
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        host.Gateway.SetPaid(topUpSessionId);
        using var status = await owner.GetAsync($"/api/payments/session/{topUpSessionId}/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        await using (var verification = host.Services.CreateAsyncScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var reservation = await db.Reservations.AsNoTracking()
                .SingleAsync(item => item.Id == reservationId);
            var payment = await db.Payments.AsNoTracking()
                .SingleAsync(item => item.ExternalSessionId == topUpSessionId);
            Assert.Equal(start, reservation.StartTime);
            Assert.Equal(end, reservation.EndTime);
            Assert.Equal(2000m, reservation.TotalPrice);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.Equal(PaymentFulfillmentStatus.RequiresResolution, payment.FulfillmentStatus);
            Assert.False(await verification.ServiceProvider.GetRequiredService<IPaymentRepository>()
                .HasPendingTargetOverlapAsync(courtId, target, target.AddHours(2)));
        }

        var laterTarget = target.AddHours(3);
        var laterQuote = await QuoteAsync(owner, reservationId, laterTarget, laterTarget.AddHours(2));
        Assert.Equal(2000m, laterQuote.PaidCredit);
        Assert.Equal(2000m, laterQuote.TopUpAmount);
    }

    [Fact]
    public async Task CheckoutLeadTime_RejectsBelow34MinutesAndAllowsExactBoundary()
    {
        var courtId = await SeedCourtAsync();
        using var authenticated = await AuthenticatedClientAsync();
        var authorization = authenticated.DefaultRequestHeaders.Authorization;
        var target = new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Unspecified);
        var sessionsBefore = host.Gateway.SessionCount;

        using (var tooCloseFactory = host.CreateFactoryWithBookingTime(
            new TestBookingTimeService(target.AddMinutes(-34).AddSeconds(1))))
        using (var tooCloseClient = tooCloseFactory.CreateClient())
        {
            tooCloseClient.DefaultRequestHeaders.Authorization = authorization;
            using var rejected = await CheckoutAsync(tooCloseClient, courtId, target, target.AddHours(1));
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            using var problem = await AssertProblemAsync(rejected, "CHECKOUT_TOO_CLOSE");
        }

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Reservations.AnyAsync(item => item.CourtId == courtId));
            Assert.False(await db.Payments.AnyAsync(item => item.Reservation.CourtId == courtId));
        }
        Assert.Equal(sessionsBefore, host.Gateway.SessionCount);

        using (var allowedFactory = host.CreateFactoryWithBookingTime(
            new TestBookingTimeService(target.AddMinutes(-34))))
        using (var allowedClient = allowedFactory.CreateClient())
        {
            allowedClient.DefaultRequestHeaders.Authorization = authorization;
            using var allowed = await CheckoutAsync(allowedClient, courtId, target, target.AddHours(1));
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        Assert.Equal(sessionsBefore + 1, host.Gateway.SessionCount);
        await using var finalScope = host.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await finalDb.Reservations.CountAsync(item => item.CourtId == courtId));
        Assert.Equal(1, await finalDb.Payments.CountAsync(item => item.Reservation.CourtId == courtId));
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
    public async Task SamePriceReschedule_DoesNotCreateTopUp()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(user, courtId, start, end);
        await PayAsync(user, session);
        var newStart = start.AddHours(2);
        var quote = await QuoteAsync(user, id, newStart, newStart.AddHours(1));
        Assert.Equal(2000m, quote.NewPrice);
        Assert.Equal(2000m, quote.PaidCredit);
        Assert.Equal(0m, quote.TopUpAmount);

        using var changed = await RescheduleAsync(user, id, newStart, newStart.AddHours(1), quote);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(newStart, (await db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id)).StartTime);
        Assert.Equal(1, await db.Payments.CountAsync(item => item.ReservationId == id));
    }

    [Fact]
    public async Task CheaperReschedule_PreservesPaidCredit_AndLaterChangeUsesIt()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, _) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(user, courtId, start, start.AddHours(2));
        await PayAsync(user, session);
        using var admin = await AuthenticatedClientAsync(admin: true);
        using var revenueBeforeResponse = await admin.GetAsync("/api/admin/stats");
        Assert.Equal(HttpStatusCode.OK, revenueBeforeResponse.StatusCode);
        using var revenueBefore = JsonDocument.Parse(await revenueBeforeResponse.Content.ReadAsStringAsync());
        var realizedBefore = revenueBefore.RootElement.GetProperty("realizedRevenue").GetDecimal();

        var shorterStart = start.AddHours(3);
        var cheaper = await QuoteAsync(user, id, shorterStart, shorterStart.AddHours(1));
        Assert.Equal(4000m, cheaper.PaidCredit);
        Assert.Equal(2000m, cheaper.NewPrice);
        Assert.Equal(2000m, cheaper.NonRefundedDifference);
        Assert.True(cheaper.RequiresNoRefundConfirmation);
        using var rejected = await RescheduleAsync(user, id, shorterStart,
            shorterStart.AddHours(1), cheaper, acknowledge: false);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var accepted = await RescheduleAsync(user, id, shorterStart,
            shorterStart.AddHours(1), cheaper, acknowledge: true);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var laterStart = start.AddHours(5);
        var later = await QuoteAsync(user, id, laterStart, laterStart.AddHours(2));
        Assert.Equal(4000m, later.PaidCredit);
        Assert.Equal(4000m, later.NewPrice);
        Assert.Equal(0m, later.TopUpAmount);
        using var laterChange = await RescheduleAsync(user, id, laterStart,
            laterStart.AddHours(2), later);
        Assert.Equal(HttpStatusCode.OK, laterChange.StatusCode);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4000m, (await db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id)).TotalPrice);
        Assert.Equal(4000m, await db.Payments.Where(item => item.ReservationId == id &&
            item.Status == PaymentStatus.Paid).SumAsync(item => item.Amount));
        Assert.Equal(1, await db.Payments.CountAsync(item => item.ReservationId == id));
        using var revenueAfterResponse = await admin.GetAsync("/api/admin/stats");
        Assert.Equal(HttpStatusCode.OK, revenueAfterResponse.StatusCode);
        using var revenueAfter = JsonDocument.Parse(await revenueAfterResponse.Content.ReadAsStringAsync());
        Assert.Equal(realizedBefore, revenueAfter.RootElement.GetProperty("realizedRevenue").GetDecimal());
    }

    [Fact]
    public async Task LongerReschedule_CreatesOnlyDifferenceAndHoldsTargetWithoutChangingOriginal()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(user, courtId, start, end);
        await PayAsync(user, session);
        var target = start.AddHours(2);
        var quote = await QuoteAsync(user, id, target, target.AddHours(2));
        Assert.Equal(4000m, quote.NewPrice);
        Assert.Equal(2000m, quote.PaidCredit);
        Assert.Equal(2000m, quote.TopUpAmount);
        using var response = await RescheduleAsync(user, id, target, target.AddHours(2), quote);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("checkoutUrl").GetString()));
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(start, original.StartTime);
        Assert.Equal(end, original.EndTime);
        Assert.Equal(2000m, original.TotalPrice);
        var topUp = await db.Payments.AsNoTracking().SingleAsync(item => item.ReservationId == id &&
            item.Purpose == PaymentPurpose.RescheduleTopUp);
        Assert.Equal(PaymentStatus.Pending, topUp.Status);
        Assert.Equal(2000m, topUp.Amount);
        Assert.Equal(target, topUp.TargetStartTime);
        using var cancelWhilePending = await user.DeleteAsync($"/api/reservations/{id}");
        Assert.Equal(HttpStatusCode.Conflict, cancelWhilePending.StatusCode);
        using var competing = await CheckoutAsync(user, courtId, target, target.AddHours(1));
        Assert.Equal(HttpStatusCode.Conflict, competing.StatusCode);
    }

    [Fact]
    public async Task PaidTopUp_AppliesOnceAndKeepsBothSuccessfulPayments()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(user, courtId, start, end);
        await PayAsync(user, session);
        var target = start.AddHours(2);
        var quote = await QuoteAsync(user, id, target, target.AddHours(2));
        using var checkout = await RescheduleAsync(user, id, target, target.AddHours(2), quote);
        var topUpSession = SessionIdFromResponse(await checkout.Content.ReadAsStringAsync());
        host.Gateway.SetPaid(topUpSession);
        using var status = await user.GetAsync($"/api/payments/session/{topUpSession}/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var duplicate = await WebhookAsync(topUpSession);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var changed = await db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(target, changed.StartTime);
        Assert.Equal(target.AddHours(2), changed.EndTime);
        Assert.Equal(4000m, changed.TotalPrice);
        Assert.Equal(2, await db.Payments.CountAsync(item => item.ReservationId == id &&
            item.Status == PaymentStatus.Paid));
        Assert.Equal(4000m, await db.Payments.Where(item => item.ReservationId == id &&
            item.Status == PaymentStatus.Paid).SumAsync(item => item.Amount));
    }

    [Fact]
    public async Task ExpiredTopUp_ReleasesTargetAndLeavesOriginalUntouched()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(user, courtId, start, end);
        await PayAsync(user, session);
        var target = start.AddHours(2);
        var quote = await QuoteAsync(user, id, target, target.AddHours(2));
        using var checkout = await RescheduleAsync(user, id, target, target.AddHours(2), quote);
        var topUpSession = SessionIdFromResponse(await checkout.Content.ReadAsStringAsync());
        await host.Gateway.ExpireSessionAsync(topUpSession);
        using var status = await user.GetAsync($"/api/payments/session/{topUpSession}/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(start, original.StartTime);
        Assert.Equal(end, original.EndTime);
        Assert.Equal("Active", original.Status);
        Assert.Equal(PaymentStatus.Cancelled, (await db.Payments.AsNoTracking()
            .SingleAsync(item => item.ExternalSessionId == topUpSession)).Status);
        using var availableAgain = await CheckoutAsync(user, courtId, target, target.AddHours(1));
        Assert.Equal(HttpStatusCode.OK, availableAgain.StatusCode);
    }

    [Fact]
    public async Task ClientCannotChooseTopUpPrice()
    {
        var courtId = await SeedCourtAsync();
        using var user = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(user, courtId, start, end);
        await PayAsync(user, session);
        var target = start.AddHours(2);
        var quote = await QuoteAsync(user, id, target, target.AddHours(2));
        using var tampered = await user.PutAsJsonAsync($"/api/reservations/{id}/reschedule", new
        {
            startTime = Local(target), endTime = Local(target.AddHours(2)),
            expectedNewPrice = quote.NewPrice, expectedTopUpAmount = 1m,
            acknowledgeNoRefund = false
        });
        Assert.Equal(HttpStatusCode.Conflict, tampered.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Payments.CountAsync(item => item.ReservationId == id));
    }

    [Fact]
    public async Task ConcurrentDuplicateTopUps_CreateOnlyOneChargeableHold()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, session);
        var target = start.AddHours(2);
        var quote = await QuoteAsync(owner, id, target, target.AddHours(2));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () => { await gate.Task; return await RescheduleAsync(owner, id, target, target.AddHours(2), quote); });
        var second = Task.Run(async () => { await gate.Task; return await RescheduleAsync(owner, id, target, target.AddHours(2), quote); });
        gate.SetResult();
        var responses = await Task.WhenAll(first, second);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Payments.CountAsync(payment => payment.ReservationId == id &&
            payment.Purpose == PaymentPurpose.RescheduleTopUp && payment.Status == PaymentStatus.Pending));
        Assert.Equal(start, (await db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id)).StartTime);
    }

    [Fact]
    public async Task SecondPaidRescheduleWhileTopUpPending_IsRejected()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        var (start, end) = Slot(9);
        var (id, session) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, session);
        var firstTarget = start.AddHours(2);
        using var first = await StartTopUpAsync(owner, id, firstTarget);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var secondTarget = start.AddHours(5);
        using var second = await RescheduleAsync(owner, id, secondTarget, secondTarget.AddHours(2),
            new QuoteValues(2000m, 4000m, 2000m, 2000m, 0m, false));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Payments.CountAsync(payment => payment.ReservationId == id &&
            payment.Purpose == PaymentPurpose.RescheduleTopUp && payment.Status == PaymentStatus.Pending));
    }

    [Fact]
    public async Task ConcurrentTopUpCompletion_AppliesOnlyOnce()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, session);
        var target = start.AddHours(2);
        using var checkout = await StartTopUpAsync(owner, id, target);
        var topUpSession = SessionIdFromResponse(await checkout.Content.ReadAsStringAsync());
        host.Gateway.SetPaid(topUpSession);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () => { await gate.Task; return await WebhookAsync(topUpSession); });
        var second = Task.Run(async () => { await gate.Task; return await WebhookAsync(topUpSession); });
        gate.SetResult();
        var responses = await Task.WhenAll(first, second);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await AssertPaidTopUpStateAsync(id, target);
    }

    [Fact]
    public async Task WebhookAndStatusReconciliationRace_ConvergeOnOneCompletion()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, session);
        var target = start.AddHours(2);
        using var checkout = await StartTopUpAsync(owner, id, target);
        var topUpSession = SessionIdFromResponse(await checkout.Content.ReadAsStringAsync());
        host.Gateway.SetPaid(topUpSession);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var webhook = Task.Run(async () => { await gate.Task; return await WebhookAsync(topUpSession); });
        var status = Task.Run(async () => { await gate.Task; return await owner.GetAsync($"/api/payments/session/{topUpSession}/status"); });
        gate.SetResult();
        using var webhookResponse = await webhook;
        using var statusResponse = await status;
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        await AssertPaidTopUpStateAsync(id, target);
    }

    [Fact]
    public async Task PendingTopUpHold_BlocksAnotherUsersCheckout()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        using var other = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, session);
        var target = start.AddHours(2);
        using var topUp = await StartTopUpAsync(owner, id, target);
        Assert.Equal(HttpStatusCode.OK, topUp.StatusCode);
        using var competing = await CheckoutAsync(other, courtId, target, target.AddHours(1));
        Assert.Equal(HttpStatusCode.Conflict, competing.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Reservations.AnyAsync(item => item.CourtId == courtId && item.StartTime == target));
    }

    [Fact]
    public async Task BackgroundReconciliation_ReleasesExpiredHoldWithoutOwnerReturn()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        using var other = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, session);
        var target = start.AddHours(2);
        using var topUp = await StartTopUpAsync(owner, id, target);
        var topUpSession = SessionIdFromResponse(await topUp.Content.ReadAsStringAsync());
        await host.Gateway.ExpireSessionAsync(topUpSession);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = await db.Payments.SingleAsync(item => item.ExternalSessionId == topUpSession);
            payment.SessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ReconcileExpiredCheckouts>().ExecuteAsync();

        using var available = await CheckoutAsync(other, courtId, target, target.AddHours(1));
        Assert.Equal(HttpStatusCode.OK, available.StatusCode);
        await using var verification = host.Services.CreateAsyncScope();
        var verifyDb = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(PaymentStatus.Cancelled, (await verifyDb.Payments.AsNoTracking()
            .SingleAsync(item => item.ExternalSessionId == topUpSession)).Status);
        Assert.Equal(start, (await verifyDb.Reservations.AsNoTracking().SingleAsync(item => item.Id == id)).StartTime);
    }

    [Fact]
    public async Task TopUpCheckoutCreationFailure_LeavesOriginalAndNoHold()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        using var other = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, session);
        var target = start.AddHours(2);
        host.Gateway.FailNextCheckout();
        using var failed = await StartTopUpAsync(owner, id, target);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        using var available = await CheckoutAsync(other, courtId, target, target.AddHours(1));
        Assert.Equal(HttpStatusCode.OK, available.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(start, (await db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id)).StartTime);
        Assert.Equal(0, await db.Payments.CountAsync(item => item.ReservationId == id &&
            item.Purpose == PaymentPurpose.RescheduleTopUp && item.Status == PaymentStatus.Pending));
    }

    [Fact]
    public async Task OtherUserCannotAccessTopUpOrRescheduleReservation()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        using var other = await AuthenticatedClientAsync();
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, session);
        var target = start.AddHours(2);
        using var topUp = await StartTopUpAsync(owner, id, target);
        var topUpSession = SessionIdFromResponse(await topUp.Content.ReadAsStringAsync());
        using var status = await other.GetAsync($"/api/payments/session/{topUpSession}/status");
        using var quote = await other.PostAsJsonAsync($"/api/reservations/{id}/reschedule/quote", new
        {
            startTime = Local(target), endTime = Local(target.AddHours(2))
        });
        using var change = await other.PutAsJsonAsync($"/api/reservations/{id}/reschedule", new
        {
            startTime = Local(target), endTime = Local(target.AddHours(2)),
            expectedNewPrice = 4000m, expectedTopUpAmount = 2000m
        });
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, quote.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
    }

    [Fact]
    public async Task TwoUsersRacingForSameTarget_CreateAtMostOneTopUpHold()
    {
        var courtId = await SeedCourtAsync();
        using var firstOwner = await AuthenticatedClientAsync();
        using var secondOwner = await AuthenticatedClientAsync();
        var (start, _) = Slot(9);
        var (firstId, firstSession) = await CreateCheckoutAsync(firstOwner, courtId, start, start.AddHours(1));
        var secondStart = start.AddHours(1);
        var (secondId, secondSession) = await CreateCheckoutAsync(secondOwner, courtId, secondStart, secondStart.AddHours(1));
        await PayAsync(firstOwner, firstSession);
        await PayAsync(secondOwner, secondSession);
        var target = start.AddHours(3);
        var firstQuote = await QuoteAsync(firstOwner, firstId, target, target.AddHours(2));
        var secondQuote = await QuoteAsync(secondOwner, secondId, target, target.AddHours(2));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () => { await gate.Task; return await RescheduleAsync(firstOwner, firstId, target, target.AddHours(2), firstQuote); });
        var second = Task.Run(async () => { await gate.Task; return await RescheduleAsync(secondOwner, secondId, target, target.AddHours(2), secondQuote); });
        gate.SetResult();
        var responses = await Task.WhenAll(first, second);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Payments.CountAsync(item => item.Purpose == PaymentPurpose.RescheduleTopUp &&
            item.Status == PaymentStatus.Pending && (item.ReservationId == firstId || item.ReservationId == secondId)));
    }

    [Fact]
    public async Task RevenueAfterTopUpAndCheaperReschedule_RemainsTotalPaid()
    {
        var courtId = await SeedCourtAsync();
        using var owner = await AuthenticatedClientAsync();
        using var admin = await AuthenticatedClientAsync(admin: true);
        var (start, end) = Slot(10);
        var (id, session) = await CreateCheckoutAsync(owner, courtId, start, end);
        await PayAsync(owner, session);
        using var topUp = await StartTopUpAsync(owner, id, start.AddHours(2));
        var topUpSession = SessionIdFromResponse(await topUp.Content.ReadAsStringAsync());
        host.Gateway.SetPaid(topUpSession);
        using var status = await owner.GetAsync($"/api/payments/session/{topUpSession}/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var beforeResponse = await admin.GetAsync("/api/admin/stats");
        using var before = JsonDocument.Parse(await beforeResponse.Content.ReadAsStringAsync());
        var revenueBefore = before.RootElement.GetProperty("realizedRevenue").GetDecimal();
        var shorter = start.AddHours(5);
        var quote = await QuoteAsync(owner, id, shorter, shorter.AddHours(1));
        Assert.Equal(4000m, quote.PaidCredit);
        using var changed = await RescheduleAsync(owner, id, shorter, shorter.AddHours(1), quote, acknowledge: true);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var afterResponse = await admin.GetAsync("/api/admin/stats");
        using var after = JsonDocument.Parse(await afterResponse.Content.ReadAsStringAsync());
        Assert.Equal(revenueBefore, after.RootElement.GetProperty("realizedRevenue").GetDecimal());
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2000m, (await db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id)).TotalPrice);
        Assert.Equal(4000m, await db.Payments.Where(item => item.ReservationId == id &&
            item.Status == PaymentStatus.Paid).SumAsync(item => item.Amount));
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
        using var problem = await AssertProblemAsync(response, "PAYMENT_REQUIRED");
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Reservations.AnyAsync(reservation => reservation.CourtId == courtId));
    }

    private sealed record QuoteValues(decimal CurrentPrice, decimal NewPrice, decimal PaidCredit,
        decimal TopUpAmount, decimal NonRefundedDifference, bool RequiresNoRefundConfirmation);

    private async Task<QuoteValues> QuoteAsync(HttpClient client, int id, DateTime start, DateTime end)
    {
        using var response = await client.PostAsJsonAsync($"/api/reservations/{id}/reschedule/quote", new
        {
            startTime = Local(start), endTime = Local(end)
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var quote = json.RootElement;
        return new(quote.GetProperty("currentPrice").GetDecimal(),
            quote.GetProperty("newPrice").GetDecimal(),
            quote.GetProperty("paidCredit").GetDecimal(),
            quote.GetProperty("topUpAmount").GetDecimal(),
            quote.GetProperty("nonRefundedDifference").GetDecimal(),
            quote.GetProperty("requiresNoRefundConfirmation").GetBoolean());
    }

    private static Task<HttpResponseMessage> RescheduleAsync(HttpClient client, int id,
        DateTime start, DateTime end, QuoteValues quote, bool acknowledge = false) =>
        client.PutAsJsonAsync($"/api/reservations/{id}/reschedule", new
        {
            startTime = Local(start), endTime = Local(end),
            expectedNewPrice = quote.NewPrice,
            expectedTopUpAmount = quote.TopUpAmount,
            acknowledgeNoRefund = acknowledge
        });

    private static Task<HttpResponseMessage> CancelAsync(
        HttpClient client, int id, bool acknowledgeNoRefund)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/reservations/{id}")
        {
            Content = JsonContent.Create(new { acknowledgeNoRefund })
        };
        return client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> StartTopUpAsync(HttpClient owner, int id, DateTime target)
    {
        var quote = await QuoteAsync(owner, id, target, target.AddHours(2));
        return await RescheduleAsync(owner, id, target, target.AddHours(2), quote);
    }

    private async Task AssertPaidTopUpStateAsync(int id, DateTime target)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await db.Reservations.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(target, reservation.StartTime);
        Assert.Equal(target.AddHours(2), reservation.EndTime);
        Assert.Equal(4000m, reservation.TotalPrice);
        Assert.Equal(1, await db.Payments.CountAsync(item => item.ReservationId == id &&
            item.Purpose == PaymentPurpose.RescheduleTopUp && item.Status == PaymentStatus.Paid));
        Assert.Equal(4000m, await db.Payments.Where(item => item.ReservationId == id &&
            item.Status == PaymentStatus.Paid).SumAsync(item => item.Amount));
    }

    private async Task PayAsync(HttpClient client, string sessionId)
    {
        host.Gateway.SetPaid(sessionId);
        using var status = await client.GetAsync($"/api/payments/session/{sessionId}/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }

    private static string SessionIdFromResponse(string responseBody)
    {
        using var json = JsonDocument.Parse(responseBody);
        return new Uri(json.RootElement.GetProperty("checkoutUrl").GetString()!)
            .Segments.Last().Trim('/');
    }

    private static Payment PaymentForCredit(int reservationId, decimal amount,
        PaymentStatus status, PaymentFulfillmentStatus fulfillmentStatus) => new()
    {
        ReservationId = reservationId,
        Amount = amount,
        Currency = "RSD",
        Status = status,
        FulfillmentStatus = fulfillmentStatus,
        Purpose = PaymentPurpose.RescheduleTopUp,
        Provider = "Stripe",
        ExternalSessionId = $"cs_test_credit_{Guid.NewGuid():N}",
        CreatedAtUtc = DateTime.UtcNow,
        SessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(35)
    };

    private async Task<int> SeedAttentionPaymentAsync(
        PaymentStatus status,
        PaymentFulfillmentStatus fulfillmentStatus,
        DateTime createdAtUtc,
        string? resolutionReasonCode = null)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            FirstName = "Admin", LastName = "Attention", Email = UniqueEmail(),
            PasswordHash = "integration-test-not-used"
        };
        var court = new Court
        {
            Name = $"Attention {Guid.NewGuid():N}", Location = "Nis",
            PricePerHour = 2000m, IsActive = true
        };
        var (start, end) = Slot(10);
        var reservation = new Reservation
        {
            User = user,
            Court = court,
            StartTime = start,
            EndTime = end,
            TotalPrice = 2000m,
            Status = status == PaymentStatus.Pending ? "PendingPayment" : "Active",
            CreatedAt = createdAtUtc
        };
        var payment = new Payment
        {
            Reservation = reservation,
            Amount = 2000m,
            Currency = "RSD",
            Status = status,
            FulfillmentStatus = fulfillmentStatus,
            ResolutionReasonCode = resolutionReasonCode,
            Purpose = PaymentPurpose.InitialBooking,
            Provider = "Stripe",
            ExternalSessionId = $"cs_test_attention_{Guid.NewGuid():N}",
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = status == PaymentStatus.Paid ? createdAtUtc.AddMinutes(1) : null,
            SessionExpiresAtUtc = createdAtUtc.AddMinutes(35)
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return payment.Id;
    }

    private async Task<int> SeedCourtAsync(
        decimal pricePerHour = 2000m,
        string location = "Nis")
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var court = new Court
        {
            Name = $"Integration {Guid.NewGuid():N}", Location = location,
            PricePerHour = pricePerHour, IsActive = true
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
