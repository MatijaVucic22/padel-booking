using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Security;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Application.Reservations.Verification;

namespace PadelBooking.Application.Reservations.CheckIn;

public enum CheckInReservationStatus
{
    Success,
    NotFound,
    InvalidToken,
    NotEligible
}

public sealed record CheckInReservationResult(
    CheckInReservationStatus Status,
    int ReservationId = 0,
    DateTime? CheckedInAtUtc = null);

public sealed class CheckInReservation(
    IReservationRepository reservations,
    IReservationVerificationTokenService tokens,
    IBookingTimeService bookingTime)
{
    public async Task<CheckInReservationResult> ExecuteAsync(
        int reservationId,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        if (!tokens.TryValidateToken(verificationToken, out var tokenReservationId) ||
            tokenReservationId != reservationId)
            return new(CheckInReservationStatus.InvalidToken);

        var state = await reservations.GetCheckInStateAsync(
            reservationId, cancellationToken);
        if (state is null)
            return new(CheckInReservationStatus.NotFound);

        if (!IsEligible(state))
            return new(CheckInReservationStatus.NotEligible);

        if (state.CheckedInAtUtc.HasValue)
            return Success(state.Id, state.CheckedInAtUtc.Value);

        var checkedInAtUtc = NormalizeToDatabasePrecision(bookingTime.UtcNow);
        if (await reservations.TrySetCheckedInAtUtcAsync(
                reservationId, checkedInAtUtc, bookingTime.Now, cancellationToken))
            return Success(reservationId, checkedInAtUtc);

        // Another Admin may have completed the same idempotent operation first.
        state = await reservations.GetCheckInStateAsync(
            reservationId, cancellationToken);
        if (state is null)
            return new(CheckInReservationStatus.NotFound);

        return IsEligible(state) && state.CheckedInAtUtc.HasValue
            ? Success(state.Id, state.CheckedInAtUtc.Value)
            : new(CheckInReservationStatus.NotEligible);
    }

    private bool IsEligible(ReservationCheckInState state) =>
        ReservationVerificationPolicy.GetStatus(
            state.Status, state.EndTime, bookingTime.Now) ==
        ReservationVerificationStatuses.Valid;

    private static CheckInReservationResult Success(int id, DateTime checkedInAtUtc) =>
        new(CheckInReservationStatus.Success, id,
            DateTime.SpecifyKind(checkedInAtUtc, DateTimeKind.Utc));

    private static DateTime NormalizeToDatabasePrecision(DateTime value) =>
        new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);
}
