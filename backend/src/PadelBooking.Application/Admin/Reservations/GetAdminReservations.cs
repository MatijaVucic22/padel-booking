using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Admin.Models;

namespace PadelBooking.Application.Admin.Reservations;

public sealed class GetAdminReservations
{
    private readonly IAdminReadRepository _repository;
    public GetAdminReservations(IAdminReadRepository repository) => _repository = repository;
    public Task<IReadOnlyList<AdminReservationItem>> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        _repository.ListReservationsAsync(cancellationToken);
}
