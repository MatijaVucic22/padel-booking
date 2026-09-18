using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Payments;

public static class CheckoutExpirationPolicy
{
    // Stripe requires at least 30 minutes; two more minutes cover clock/network delay.
    private static readonly TimeSpan MinimumProviderLifetime = TimeSpan.FromMinutes(32);
    private static readonly TimeSpan BeforeBookingStart = TimeSpan.FromMinutes(1);
    // Leave another minute between Application validation and the gateway check.
    private static readonly TimeSpan MinimumCheckoutLeadTime = TimeSpan.FromMinutes(34);
    private static readonly TimeSpan MaximumCheckoutLifetime = TimeSpan.FromMinutes(35);

    public static DateTime? GetExpirationUtc(DateTime targetStartLocal, IBookingTimeService bookingTime)
    {
        var nowUtc = bookingTime.UtcNow;
        var targetStartUtc = bookingTime.ToUtc(targetStartLocal);
        if (targetStartUtc < nowUtc + MinimumCheckoutLeadTime) return null;
        var latestExpiration = targetStartUtc - BeforeBookingStart;

        var ordinaryExpiration = nowUtc + MaximumCheckoutLifetime;
        return latestExpiration < ordinaryExpiration ? latestExpiration : ordinaryExpiration;
    }

    public static bool CanCreateSession(DateTime expiresAtUtc, DateTime nowUtc) =>
        expiresAtUtc >= nowUtc + MinimumProviderLifetime;
}

public sealed class CheckoutWindowClosedException : Exception { }
