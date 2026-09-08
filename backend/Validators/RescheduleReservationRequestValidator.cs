using FluentValidation;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Services;

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
