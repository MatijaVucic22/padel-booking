using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Courts.DeactivateCourt;

public sealed class DeactivateCourt
{
    private readonly ICourtRepository _courts;
    private readonly IReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourtAdvisoryLockService _courtLock;
    private readonly IBookingTimeService _bookingTime;
    private readonly ICourtChangeNotifier _notifier;

    public DeactivateCourt(
        ICourtRepository courts,
        IReservationRepository reservations,
        IUnitOfWork unitOfWork,
        ICourtAdvisoryLockService courtLock,
        IBookingTimeService bookingTime,
        ICourtChangeNotifier notifier)
    {
        _courts = courts;
        _reservations = reservations;
        _unitOfWork = unitOfWork;
        _courtLock = courtLock;
        _bookingTime = bookingTime;
        _notifier = notifier;
    }

    public async Task<DeactivateCourtResult> ExecuteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var acquiredLock = await _courtLock.TryAcquireAsync(id, cancellationToken);
        if (acquiredLock is null)
        {
            return new(DeactivateCourtStatus.LockTimeout);
        }

        await using (acquiredLock)
        {
            var court = await _courts.GetByIdAsync(id, cancellationToken);
            if (court is null)
            {
                return new(DeactivateCourtStatus.NotFound);
            }

            if (await _reservations.HasFutureActiveReservationsAsync(
                    id,
                    _bookingTime.Now,
                    cancellationToken))
            {
                return new(DeactivateCourtStatus.HasFutureReservations);
            }

            court.IsActive = false;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _notifier.NotifyCourtChangedAsync(
            id,
            "deactivated",
            cancellationToken);

        return new(DeactivateCourtStatus.Success);
    }
}
