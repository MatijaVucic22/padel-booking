namespace PadelBooking.Application.Abstractions.Concurrency;

public interface ICourtAdvisoryLockService
{
    Task<IAsyncDisposable?> TryAcquireAsync(
        int courtId,
        CancellationToken cancellationToken = default
    );
}
