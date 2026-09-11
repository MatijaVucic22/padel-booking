namespace PadelBooking.Application.Abstractions.Storage;

public interface ICourtImageStorage
{
    Task<string> StoreAsync(
        CourtImageUpload image,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string imageUrl,
        CancellationToken cancellationToken = default);
}
