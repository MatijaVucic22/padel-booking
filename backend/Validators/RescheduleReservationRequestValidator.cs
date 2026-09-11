using FluentValidation;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Api.DTOs;

namespace PadelBooking.Api.Validators
{
    public class RescheduleReservationRequestValidator
        : AbstractValidator<RescheduleReservationRequest>
    {
        public RescheduleReservationRequestValidator(IBookingTimeService bookingTime)
        {
            Include(
                new ReservationTimeRequestValidator<RescheduleReservationRequest>(
                    bookingTime));
        }
    }
}
