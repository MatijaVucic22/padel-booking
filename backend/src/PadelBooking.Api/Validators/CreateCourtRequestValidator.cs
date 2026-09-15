using FluentValidation;
using PadelBooking.Api.DTOs;

namespace PadelBooking.Api.Validators
{
    public class CreateCourtRequestValidator : AbstractValidator<CreateCourtRequest>
    {
        private const long MaximumImageSize = 5 * 1024 * 1024;
        private static readonly string[] AllowedContentTypes =
        [
            "image/jpeg",
            "image/png",
            "image/webp"
        ];

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

            When(request => request.Image != null, () =>
            {
                RuleFor(request => request.Image!)
                    .Cascade(CascadeMode.Stop)
                    .Must(image => image.Length > 0)
                    .WithMessage("Izabrana slika je prazna.")
                    .Must(image => image.Length <= MaximumImageSize)
                    .WithMessage("Slika ne sme biti veća od 5 MB.")
                    .Must(image => AllowedContentTypes.Contains(
                        image.ContentType,
                        StringComparer.OrdinalIgnoreCase))
                    .WithMessage("Dozvoljeni formati slike su JPG, PNG i WebP.")
                    .Must(HasAllowedExtension)
                    .WithMessage("Dozvoljeni formati slike su JPG, PNG i WebP.")
                    .MustAsync(HasValidFileSignatureAsync)
                    .WithMessage("Sadržaj slike nije validan JPG, PNG ili WebP fajl.");
            });
        }

        private static bool HasAllowedExtension(IFormFile image)
        {
            var extension = Path.GetExtension(image.FileName);
            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> HasValidFileSignatureAsync(
            IFormFile image,
            CancellationToken cancellationToken)
        {
            var header = new byte[12];

            await using var stream = image.OpenReadStream();
            var bytesRead = await stream.ReadAsync(header, cancellationToken);

            if (bytesRead >= 3 &&
                image.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            }

            if (bytesRead >= 8 &&
                image.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
            {
                ReadOnlySpan<byte> pngSignature =
                    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
                return header.AsSpan(0, 8).SequenceEqual(pngSignature);
            }

            return bytesRead >= 12 &&
                image.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        }
    }
}
