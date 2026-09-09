using FluentValidation;
using PadelBooking.Api.DTOs;

namespace PadelBooking.Api.Validators
{
    public class CreateBlockedPeriodRequestValidator
        : AbstractValidator<CreateBlockedPeriodRequest>
    {
        public CreateBlockedPeriodRequestValidator()
        {
            RuleFor(request => request.CourtId)
                .GreaterThan(0)
                .WithMessage("Teren nije ispravan.");
            RuleFor(request => request.StartTime)
                .Must(IsFullHour)
                .WithMessage("Vreme početka mora biti na pun sat.");
            RuleFor(request => request.StartTime)
                .Must(value => value.Hour >= 8 && value.Hour < 22)
                .WithMessage("Blokirani period mora početi između 08:00 i 21:00.");
            RuleFor(request => request.EndTime)
                .GreaterThan(request => request.StartTime)
                .WithMessage("Vreme završetka mora biti posle vremena početka.");
            RuleFor(request => request.EndTime)
                .Must(IsFullHour)
                .WithMessage("Vreme završetka mora biti na pun sat.");
            RuleFor(request => request.EndTime)
                .Must((request, value) => value.Date == request.StartTime.Date)
                .WithMessage("Blokirani period mora biti u okviru istog dana.");
            RuleFor(request => request.EndTime)
                .Must(value => value.TimeOfDay <= TimeSpan.FromHours(22))
                .WithMessage("Blokirani period mora završiti najkasnije u 22:00.");
            RuleFor(request => request.Reason)
                .NotEmpty()
                .WithMessage("Razlog je obavezan.")
                .MaximumLength(300)
                .WithMessage("Razlog može imati najviše 300 karaktera.");
        }

        private static bool IsFullHour(DateTime value) =>
            value.TimeOfDay.Ticks % TimeSpan.TicksPerHour == 0;
    }
}
