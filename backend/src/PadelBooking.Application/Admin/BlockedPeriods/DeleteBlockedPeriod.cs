using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;

namespace PadelBooking.Application.Admin.BlockedPeriods;

public sealed class DeleteBlockedPeriod
{
    private readonly IBlockedPeriodRepository _blockedPeriods;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourtAdvisoryLockService _courtLock;
    private readonly ICourtChangeNotifier _notifier;

    public DeleteBlockedPeriod(IBlockedPeriodRepository blockedPeriods,
        IUnitOfWork unitOfWork, ICourtAdvisoryLockService courtLock,
        ICourtChangeNotifier notifier)
    {
        _blockedPeriods = blockedPeriods; _unitOfWork = unitOfWork;
        _courtLock = courtLock; _notifier = notifier;
    }

    public async Task<DeleteBlockedPeriodResult> ExecuteAsync(
        int id, CancellationToken cancellationToken = default)
    {
        var courtId = await _blockedPeriods.GetCourtIdAsync(id, cancellationToken);
        if (courtId is null) return new(DeleteBlockedPeriodStatus.NotFound);
        var acquiredLock = await _courtLock.TryAcquireAsync(
            courtId.Value, cancellationToken);
        if (acquiredLock is null) return new(DeleteBlockedPeriodStatus.LockTimeout);

        DateTime startTime;
        await using (acquiredLock)
        {
            var blockedPeriod = await _blockedPeriods.GetByIdAsync(id, cancellationToken);
            if (blockedPeriod is null) return new(DeleteBlockedPeriodStatus.NotFound);
            startTime = blockedPeriod.StartTime;
            _blockedPeriods.Remove(blockedPeriod);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _notifier.NotifyAvailabilityChangedAsync(
            courtId.Value, startTime, cancellationToken);
        return new(DeleteBlockedPeriodStatus.Success);
    }
}
