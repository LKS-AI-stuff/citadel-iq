using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Infrastructure.Persistence;

public class EfUnitOfWork(CitadelIQDbContext db) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await work();
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await work();
        await transaction.CommitAsync(CancellationToken.None);
    }
}
