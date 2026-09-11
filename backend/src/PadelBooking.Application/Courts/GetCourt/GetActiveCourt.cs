using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Courts.GetCourt;

public sealed class GetActiveCourt
{
    private readonly ICourtRepository _courts;

    public GetActiveCourt(ICourtRepository courts) => _courts = courts;

    public Task<Court?> ExecuteAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _courts.GetActiveByIdAsync(id, cancellationToken);
}
