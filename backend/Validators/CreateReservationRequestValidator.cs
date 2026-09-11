using FluentValidation;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Api.DTOs;

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
