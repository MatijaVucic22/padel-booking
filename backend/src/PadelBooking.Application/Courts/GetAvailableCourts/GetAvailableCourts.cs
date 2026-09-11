using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Courts.GetAvailableCourts;

public sealed class GetAvailableCourts
{
    private readonly ICourtRepository _courts;

    public GetAvailableCourts(ICourtRepository courts) => _courts = courts;

    public Task<IReadOnlyList<Court>> ExecuteAsync(
        DateTime startTime,
        int durationHours,
        CancellationToken cancellationToken = default) =>
        _courts.ListAvailableAsync(
            startTime,
            startTime.AddHours(durationHours),
            cancellationToken);
}
