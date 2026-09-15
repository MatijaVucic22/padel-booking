using FluentValidation;
using PadelBooking.Api.DTOs;

namespace PadelBooking.Api.Validators
{
    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(request => request.Email)
                .NotEmpty().WithMessage("Email je obavezan.");

            RuleFor(request => request.Password)
                .NotEmpty().WithMessage("Lozinka je obavezna.");
        }
    }
}
