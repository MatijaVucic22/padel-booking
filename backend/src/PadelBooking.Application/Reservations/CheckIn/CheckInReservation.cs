using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Security;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Reservations.CheckIn;

public enum CheckInReservationStatus
{
    Success,
    NotFound,
    InvalidToken,
    TooEarly,
    WindowClosed,
    NotEligible
}

public sealed record CheckInReservationResult(
    CheckInReservationStatus Status,
    int ReservationId = 0,
    DateTime? CheckedInAtUtc = null,
    DateTime? CheckInAvailableFrom = null);

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

        var now = bookingTime.Now;
        var eligibility = GetEligibility(state, now);
        if (eligibility.Status != CheckInReservationStatus.Success)
            return eligibility;

        if (state.CheckedInAtUtc.HasValue)
            return Success(state.Id, state.CheckedInAtUtc.Value);

        var checkedInAtUtc = NormalizeToDatabasePrecision(bookingTime.UtcNow);
        if (await reservations.TrySetCheckedInAtUtcAsync(
                reservationId, checkedInAtUtc, now,
                now.Add(ReservationCheckInPolicy.EarlyCheckInWindow), cancellationToken))
            return Success(reservationId, checkedInAtUtc);

        // Another Admin may have completed the same idempotent operation first.
        state = await reservations.GetCheckInStateAsync(
            reservationId, cancellationToken);
        if (state is null)
            return new(CheckInReservationStatus.NotFound);

        eligibility = GetEligibility(state, now);
        return eligibility.Status == CheckInReservationStatus.Success &&
               state.CheckedInAtUtc.HasValue
            ? Success(state.Id, state.CheckedInAtUtc.Value)
            : eligibility.Status == CheckInReservationStatus.Success
                ? new(CheckInReservationStatus.NotEligible)
                : eligibility;
    }

    private static CheckInReservationResult GetEligibility(
        ReservationCheckInState state,
        DateTime now)
    {
        if (!string.Equals(state.Status, "Active", StringComparison.Ordinal))
            return new(CheckInReservationStatus.NotEligible);

        return ReservationCheckInPolicy.GetStatus(state.StartTime, state.EndTime, now) switch
        {
            ReservationCheckInWindowStatus.TooEarly => new(
                CheckInReservationStatus.TooEarly,
                CheckInAvailableFrom: ReservationCheckInPolicy.GetAvailableFrom(state.StartTime)),
            ReservationCheckInWindowStatus.Closed => new(
                CheckInReservationStatus.WindowClosed),
            _ => new(CheckInReservationStatus.Success)
        };
    }

    private static CheckInReservationResult Success(int id, DateTime checkedInAtUtc) =>
        new(CheckInReservationStatus.Success, id,
            DateTime.SpecifyKind(checkedInAtUtc, DateTimeKind.Utc));

    private static DateTime NormalizeToDatabasePrecision(DateTime value) =>
        new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);
}
