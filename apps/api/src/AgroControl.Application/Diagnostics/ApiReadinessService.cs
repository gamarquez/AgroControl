namespace AgroControl.Application.Diagnostics;

internal sealed class ApiReadinessService(
    IAgroControlDatabaseProbe databaseProbe,
    TimeProvider timeProvider) : IApiReadinessService
{
    public async Task<ApiReadinessSnapshot> GetSnapshotAsync(
        string environmentName,
        CancellationToken cancellationToken)
    {
        var database = await databaseProbe.ProbeAsync(cancellationToken);
        var dependencies = new[]
        {
            new ApiReadinessDependency(
                "postgres",
                database.IsHealthy,
                database.Description,
                database.Duration)
        };

        return new ApiReadinessSnapshot(
            dependencies.All(dependency => dependency.IsHealthy),
            environmentName,
            timeProvider.GetUtcNow(),
            dependencies);
    }
}
