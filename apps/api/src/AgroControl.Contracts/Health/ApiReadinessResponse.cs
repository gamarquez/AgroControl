namespace AgroControl.Contracts.Health;

public sealed record ApiReadinessResponse(
    string Status,
    string Service,
    string Environment,
    DateTimeOffset CheckedAt,
    IReadOnlyList<ApiDependencyHealthResponse> Dependencies);
