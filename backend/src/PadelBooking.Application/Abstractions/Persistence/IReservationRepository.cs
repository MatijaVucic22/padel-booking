using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Abstractions.Persistence;

public interface IReservationRepository
{
    Task<ReservationCheckInState?> GetCheckInStateAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> TrySetCheckedInAtUtcAsync(
        int id,
        DateTime checkedInAtUtc,
        DateTime currentBookingTime,
        DateTime latestEligibleStartTime,
        CancellationToken cancellationToken = default);

    Task<Reservation?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Reservation?> GetByIdForUserAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Reservation?> GetForVerificationAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<int?> GetCourtIdForUserAsync(
        int id,
        int userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Reservation>> GetForUserAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlapAsync(
        int courtId,
        DateTime requestedStart,
        DateTime requestedEnd,
        int? excludedReservationId = null,
        CancellationToken cancellationToken = default);

    Task<bool> HasFutureActiveReservationsAsync(
        int courtId,
        DateTime now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Reservation>> ListOverlappingAsync(
        int courtId,
        DateTime requestedStart,
        DateTime requestedEnd,
        int? excludedReservationId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CourtScheduleInterval>> ListOverlappingForCourtsAsync(
        IReadOnlyCollection<int> courtIds,
        DateTime requestedStart,
        DateTime requestedEnd,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> ListDueReminderIdsAsync(
        DateTime now,
        DateTime reminderCutoff,
        CancellationToken cancellationToken = default);

    void Add(Reservation reservation);
}

public sealed record ReservationCheckInState(
    int Id,
    string Status,
    DateTime StartTime,
    DateTime EndTime,
    DateTime? CheckedInAtUtc);
