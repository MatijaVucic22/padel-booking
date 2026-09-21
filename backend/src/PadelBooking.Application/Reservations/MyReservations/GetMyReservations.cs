using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Reservations.MyReservations;

public sealed class GetMyReservations
{
    private readonly IReservationRepository _reservations;
    private readonly IPaymentRepository _payments;
    private readonly IBookingTimeService _bookingTime;

    public GetMyReservations(
        IReservationRepository reservations,
        IPaymentRepository payments,
        IBookingTimeService bookingTime)
    {
        _reservations = reservations;
        _payments = payments;
        _bookingTime = bookingTime;
    }

    public async Task<IReadOnlyList<MyReservationItem>> ExecuteAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var now = _bookingTime.Now;
        var reservations = await _reservations.GetForUserAsync(userId, cancellationToken);
        var visibleReservations = reservations
            .Where(reservation => reservation.Status != "PendingPayment")
            .ToList();
        var paidAmounts = await _payments.GetAppliedPaidAmountsAsync(
            visibleReservations.Select(reservation => reservation.Id).ToArray(),
            cancellationToken);

        return visibleReservations
            .Select(reservation => new MyReservationItem(
            reservation.Id, reservation.CourtId, reservation.Court.Name,
            reservation.StartTime, reservation.EndTime, reservation.TotalPrice,
            paidAmounts.GetValueOrDefault(reservation.Id),
            reservation.Status != "Cancelled" && reservation.StartTime > now,
            reservation.Status)).ToList();
    }
}
