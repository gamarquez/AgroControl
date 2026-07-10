namespace AgroControl.Contracts.Health;

public sealed record ApiDependencyHealthResponse(
    string Name,
    string Status,
    string Description,
    long DurationMilliseconds);
