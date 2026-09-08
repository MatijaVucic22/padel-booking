using FluentValidation;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Services;

namespace PadelBooking.Api.Validators
{
    public class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
    {
        public CreateReservationRequestValidator(IBookingTimeService bookingTime)
        {
            RuleFor(request => request.CourtId)
                .GreaterThan(0).WithMessage("CourtId mora biti veći od 0.");

            Include(
                new ReservationTimeRequestValidator<CreateReservationRequest>(
                    bookingTime));
        }
    }
}
