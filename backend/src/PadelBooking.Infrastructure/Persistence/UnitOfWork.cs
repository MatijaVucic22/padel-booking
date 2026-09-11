using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using PadelBooking.Application.Abstractions.Persistence;

namespace PadelBooking.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is MySqlException { Number: 1062 })
        {
            throw new UniqueConstraintViolationException(
                "A unique database constraint was violated.", exception);
        }
    }
}
