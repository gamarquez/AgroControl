namespace AgroControl.Application.Diagnostics;

public sealed record ApiReadinessSnapshot(
    bool IsHealthy,
    string Environment,
    DateTimeOffset CheckedAt,
    IReadOnlyList<ApiReadinessDependency> Dependencies);
