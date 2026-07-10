namespace AgroControl.Application.Diagnostics;

public interface IApiReadinessService
{
    Task<ApiReadinessSnapshot> GetSnapshotAsync(string environmentName, CancellationToken cancellationToken);
}
