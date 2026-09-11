using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Time;

namespace PadelBooking.Application.Reservations.MyReservations;

public sealed class GetMyReservations
{
    private readonly IReservationRepository _reservations;
    private readonly IBookingTimeService _bookingTime;

    public GetMyReservations(
        IReservationRepository reservations,
        IBookingTimeService bookingTime)
    {
        _reservations = reservations;
        _bookingTime = bookingTime;
    }

    public async Task<IReadOnlyList<MyReservationItem>> ExecuteAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var now = _bookingTime.Now;
        var reservations = await _reservations.GetForUserAsync(userId, cancellationToken);
        return reservations.Select(reservation => new MyReservationItem(
            reservation.Id, reservation.CourtId, reservation.Court.Name,
            reservation.StartTime, reservation.EndTime, reservation.TotalPrice,
            reservation.Status != "Cancelled" && reservation.StartTime > now,
            reservation.Status)).ToList();
    }
}
