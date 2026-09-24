using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Payments;

public static class CheckoutExpirationPolicy
{
    // Stripe requires at least 30 minutes; two more minutes cover clock/network delay.
    private static readonly TimeSpan MinimumProviderLifetime = TimeSpan.FromMinutes(32);
    private static readonly TimeSpan MaximumCheckoutLifetime = TimeSpan.FromMinutes(35);

    public static DateTime GetExpirationUtc(IBookingTimeService bookingTime) =>
        bookingTime.UtcNow + MaximumCheckoutLifetime;

    public static bool CanCreateSession(DateTime expiresAtUtc, DateTime nowUtc) =>
        expiresAtUtc >= nowUtc + MinimumProviderLifetime;
}

public sealed class CheckoutWindowClosedException : Exception { }
