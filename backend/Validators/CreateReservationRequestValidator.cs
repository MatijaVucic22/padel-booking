using FluentValidation;
using PadelBooking.Api.DTOs;

namespace PadelBooking.Api.Validators
{
    public class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
    {
        public CreateReservationRequestValidator()
        {
            RuleFor(request => request.CourtId)
                .GreaterThan(0).WithMessage("CourtId mora biti veći od 0.");

            RuleFor(request => request.StartTime)
                .Must(startTime => startTime > DateTime.Now)
                .WithMessage("Vreme početka mora biti u budućnosti.");

            RuleFor(request => request.EndTime)
                .GreaterThan(request => request.StartTime)
                .WithMessage("Vreme završetka mora biti posle vremena početka.");
        }
    }
}
