using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Courts.UpdateCourt;

public sealed class UpdateCourt
{
    private readonly ICourtRepository _courts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourtChangeNotifier _notifier;

    public UpdateCourt(
        ICourtRepository courts,
        IUnitOfWork unitOfWork,
        ICourtChangeNotifier notifier)
    {
        _courts = courts;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task<Court?> ExecuteAsync(
        UpdateCourtCommand command,
        CancellationToken cancellationToken = default)
    {
        var court = await _courts.GetByIdAsync(command.Id, cancellationToken);
        if (court is null) return null;

        court.Name = command.Name.Trim();
        court.Location = command.Location.Trim();
        court.Description = command.Description?.Trim();
        court.PricePerHour = command.PricePerHour;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyCourtChangedAsync(
            court.Id,
            "updated",
            cancellationToken);

        return court;
    }
}
