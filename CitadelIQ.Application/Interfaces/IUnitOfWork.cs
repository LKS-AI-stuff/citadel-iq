namespace CitadelIQ.Application.Interfaces;

public interface IUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in one database transaction (joining an already-open one).</summary>
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken cancellationToken = default);
}
