using FluentValidation;
using PadelBooking.Api.DTOs;

namespace PadelBooking.Api.Validators
{
    public class CreateCourtRequestValidator : AbstractValidator<CreateCourtRequest>
    {
        public CreateCourtRequestValidator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("Naziv terena je obavezan.")
                .Length(2, 100).WithMessage("Naziv terena mora imati između 2 i 100 karaktera.");

            RuleFor(request => request.Location)
                .NotEmpty().WithMessage("Lokacija je obavezna.")
                .Length(2, 100).WithMessage("Lokacija mora imati između 2 i 100 karaktera.");

            RuleFor(request => request.Description)
                .MaximumLength(500).WithMessage("Opis može imati najviše 500 karaktera.");

            RuleFor(request => request.PricePerHour)
                .GreaterThan(0).WithMessage("Cena po satu mora biti veća od 0.");
        }
    }
}
