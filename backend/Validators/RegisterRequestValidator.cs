using FluentValidation;
using PadelBooking.Api.DTOs;

namespace PadelBooking.Api.Validators
{
    public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            RuleFor(request => request.FirstName)
                .NotEmpty().WithMessage("Ime je obavezno.")
                .Length(2, 50).WithMessage("Ime mora imati između 2 i 50 karaktera.");

            RuleFor(request => request.LastName)
                .NotEmpty().WithMessage("Prezime je obavezno.")
                .Length(2, 50).WithMessage("Prezime mora imati između 2 i 50 karaktera.");

            RuleFor(request => request.Email)
                .NotEmpty().WithMessage("Email je obavezan.")
                .EmailAddress().WithMessage("Email format nije ispravan.");

            RuleFor(request => request.Password)
                .NotEmpty().WithMessage("Lozinka je obavezna.")
                .MinimumLength(8).WithMessage("Lozinka mora imati najmanje 8 karaktera.")
                .Matches("[A-Z]").WithMessage("Lozinka mora sadržati najmanje jedno veliko slovo.")
                .Matches("[a-z]").WithMessage("Lozinka mora sadržati najmanje jedno malo slovo.")
                .Matches("[0-9]").WithMessage("Lozinka mora sadržati najmanje jedan broj.");
        }
    }
}
