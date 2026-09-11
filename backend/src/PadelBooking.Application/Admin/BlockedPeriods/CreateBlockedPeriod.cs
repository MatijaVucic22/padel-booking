using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Admin.BlockedPeriods;

public sealed class CreateBlockedPeriod
{
    private readonly ICourtRepository _courts;
    private readonly IReservationRepository _reservations;
    private readonly IBlockedPeriodRepository _blockedPeriods;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourtAdvisoryLockService _courtLock;
    private readonly IBookingTimeService _bookingTime;
    private readonly ICourtChangeNotifier _notifier;

    public CreateBlockedPeriod(ICourtRepository courts,
        IReservationRepository reservations, IBlockedPeriodRepository blockedPeriods,
        IUnitOfWork unitOfWork, ICourtAdvisoryLockService courtLock,
        IBookingTimeService bookingTime, ICourtChangeNotifier notifier)
    {
        _courts = courts; _reservations = reservations;
        _blockedPeriods = blockedPeriods; _unitOfWork = unitOfWork;
        _courtLock = courtLock; _bookingTime = bookingTime; _notifier = notifier;
    }

    public async Task<CreateBlockedPeriodResult> ExecuteAsync(
        CreateBlockedPeriodCommand command,
        CancellationToken cancellationToken = default)
    {
        var acquiredLock = await _courtLock.TryAcquireAsync(
            command.CourtId, cancellationToken);
        if (acquiredLock is null) return new(CreateBlockedPeriodStatus.LockTimeout);

        BlockedPeriod blockedPeriod;
        string courtName;
        await using (acquiredLock)
        {
            var court = await _courts.GetActiveByIdAsync(
                command.CourtId, cancellationToken);
            if (court is null) return new(CreateBlockedPeriodStatus.CourtNotFound);
            courtName = court.Name;

            if (await _reservations.HasOverlapAsync(command.CourtId,
                    command.StartTime, command.EndTime, null, cancellationToken))
                return new(CreateBlockedPeriodStatus.ReservationOverlap);
            if (await _blockedPeriods.HasOverlapAsync(command.CourtId,
                    command.StartTime, command.EndTime, cancellationToken))
                return new(CreateBlockedPeriodStatus.BlockedPeriodOverlap);

            blockedPeriod = new BlockedPeriod
            {
                CourtId = command.CourtId,
                StartTime = command.StartTime,
                EndTime = command.EndTime,
                Reason = command.Reason.Trim(),
                CreatedAtUtc = _bookingTime.UtcNow
            };
            _blockedPeriods.Add(blockedPeriod);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _notifier.NotifyAvailabilityChangedAsync(
            blockedPeriod.CourtId, blockedPeriod.StartTime, cancellationToken);
        return new(CreateBlockedPeriodStatus.Success,
            new CreatedBlockedPeriod(blockedPeriod.Id, blockedPeriod.CourtId,
                courtName, blockedPeriod.StartTime, blockedPeriod.EndTime,
                blockedPeriod.Reason));
    }
}
