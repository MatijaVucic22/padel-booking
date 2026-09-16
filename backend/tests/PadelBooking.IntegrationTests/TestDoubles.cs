using System.Collections.Concurrent;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Application.Notifications;

namespace PadelBooking.IntegrationTests;

public sealed class TestPaymentGateway : IPaymentGateway
{
    private sealed record FakeSession(string Id, string Email, string Reference,
        long AmountMinor, string Currency, string State);

    private readonly ConcurrentDictionary<string, FakeSession> _sessions = new();

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = $"cs_test_integration_{Guid.NewGuid():N}";
        _sessions[id] = new FakeSession(id, request.CustomerEmail,
            request.CheckoutReference, request.AmountMinor, request.Currency, "open");
        return Task.FromResult(new CheckoutSession(id,
            $"https://checkout.stripe.test/session/{id}", DateTime.UtcNow.AddMinutes(35)));
    }

    public Task<CheckoutEvent> GetSessionAsync(string sessionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ToEvent(_sessions[sessionId]));

    public Task ExpireSessionAsync(string sessionId,
        CancellationToken cancellationToken = default)
    {
        _sessions.AddOrUpdate(sessionId, _ => throw new KeyNotFoundException(),
            (_, session) => session with { State = "expired" });
        return Task.CompletedTask;
    }

    public void SetPaid(string sessionId) =>
        _sessions.AddOrUpdate(sessionId, _ => throw new KeyNotFoundException(),
            (_, session) => session with { State = "paid" });

    public CheckoutEvent VerifyWebhook(string rawBody, string signature)
    {
        if (signature != "integration-test") throw new InvalidPaymentWebhookException();
        return ToEvent(_sessions[rawBody]);
    }

    private static CheckoutEvent ToEvent(FakeSession session) => new(
        session.State switch
        {
            "paid" => "checkout.session.completed",
            "expired" => "checkout.session.expired",
            _ => "checkout.session.open"
        },
        session.Id,
        session.State == "paid" ? $"pi_test_{session.Id}" : null,
        session.State == "paid" ? "paid" : "unpaid",
        session.AmountMinor,
        session.Currency,
        session.Email,
        session.Reference,
        session.Reference);
}

public sealed class TestEmailService : IEmailService
{
    private readonly ConcurrentDictionary<int, int> _confirmations = new();
    public int ConfirmationCount(int reservationId) =>
        _confirmations.TryGetValue(reservationId, out var count) ? count : 0;

    public Task SendReservationConfirmationAsync(ReservationConfirmationEmail confirmation,
        CancellationToken cancellationToken = default)
    {
        _confirmations.AddOrUpdate(confirmation.ReservationId, 1, (_, value) => value + 1);
        return Task.CompletedTask;
    }

    public Task SendReservationCancellationAsync(ReservationCancellationEmail cancellation,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendReservationReminderAsync(ReservationReminderEmail reminder,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendReservationRescheduledAsync(ReservationRescheduledEmail rescheduled,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class TestCourtChangeNotifier : ICourtChangeNotifier
{
    public Task NotifyCourtChangedAsync(int courtId, string changeType,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyAvailabilityChangedAsync(int courtId, DateTime date,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class TestNotificationLogger : IReservationNotificationLogger
{
    public void LogEmailFailure(Exception exception, int reservationId,
        string notificationType, bool warning = false) { }
}
