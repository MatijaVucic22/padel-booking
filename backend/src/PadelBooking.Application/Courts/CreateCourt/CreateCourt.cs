using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Storage;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Courts.CreateCourt;

public sealed class CreateCourt
{
    private readonly ICourtRepository _courts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourtImageStorage _imageStorage;
    private readonly ICourtChangeNotifier _notifier;

    public CreateCourt(
        ICourtRepository courts,
        IUnitOfWork unitOfWork,
        ICourtImageStorage imageStorage,
        ICourtChangeNotifier notifier)
    {
        _courts = courts;
        _unitOfWork = unitOfWork;
        _imageStorage = imageStorage;
        _notifier = notifier;
    }

    public async Task<Court> ExecuteAsync(
        CreateCourtCommand command,
        CancellationToken cancellationToken = default)
    {
        string? imageUrl = null;
        if (command.Image is not null)
        {
            imageUrl = await _imageStorage.StoreAsync(
                command.Image,
                cancellationToken);
        }

        var court = new Court
        {
            Name = command.Name.Trim(),
            Location = command.Location.Trim(),
            Description = command.Description?.Trim(),
            PricePerHour = command.PricePerHour,
            ImageUrl = imageUrl,
            IsActive = true
        };

        try
        {
            _courts.Add(court);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (imageUrl is not null)
            {
                await _imageStorage.DeleteAsync(imageUrl, CancellationToken.None);
            }

            throw;
        }

        await _notifier.NotifyCourtChangedAsync(
            court.Id,
            "created",
            cancellationToken);

        return court;
    }
}
