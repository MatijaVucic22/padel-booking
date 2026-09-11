using Microsoft.Extensions.Options;
using PadelBooking.Application.Abstractions.Storage;

namespace PadelBooking.Infrastructure.Storage;

public sealed class LocalCourtImageStorage : ICourtImageStorage
{
    private readonly string _rootPath;

    public LocalCourtImageStorage(IOptions<CourtImageStorageOptions> options)
    {
        _rootPath = options.Value.RootPath;
    }

    public async Task<string> StoreAsync(
        CourtImageUpload image,
        CancellationToken cancellationToken = default)
    {
        var extension = image.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException(
                "Nepodržan tip slike prošao je validaciju.")
        };

        Directory.CreateDirectory(_rootPath);
        var generatedFileName = $"{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(_rootPath, generatedFileName);

        try
        {
            await using var output = new FileStream(
                storedPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            await image.Content.CopyToAsync(output, cancellationToken);
        }
        catch
        {
            if (File.Exists(storedPath)) File.Delete(storedPath);
            throw;
        }

        return $"/uploads/courts/{generatedFileName}";
    }

    public Task DeleteAsync(
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileName = Path.GetFileName(imageUrl);
        if (!string.IsNullOrEmpty(fileName))
        {
            var storedPath = Path.Combine(_rootPath, fileName);
            if (File.Exists(storedPath)) File.Delete(storedPath);
        }

        return Task.CompletedTask;
    }
}
