namespace CitadelIQ.Application.Interfaces;

/// <summary>Optional reachability check implemented by remote storage providers (used by the storage health endpoint).</summary>
public interface IStorageProbe
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
