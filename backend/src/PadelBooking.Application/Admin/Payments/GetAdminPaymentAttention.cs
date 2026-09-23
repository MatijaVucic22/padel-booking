using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Admin.Payments;

public sealed class GetAdminPaymentAttention(
    IAdminReadRepository repository,
    IBookingTimeService bookingTime)
{
    public static readonly TimeSpan PendingAgeThreshold = TimeSpan.FromMinutes(60);

    public Task<AdminPaymentAttentionPage> ExecuteAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var pendingBeforeUtc = bookingTime.UtcNow - PendingAgeThreshold;

        return repository.GetPaymentsNeedingAttentionAsync(
            pendingBeforeUtc, normalizedPage, normalizedPageSize, cancellationToken);
    }
}
