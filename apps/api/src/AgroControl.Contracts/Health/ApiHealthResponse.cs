namespace AgroControl.Contracts.Health;

public sealed record ApiHealthResponse(
    string Status,
    string Service,
    string Version,
    string Environment,
    DateTimeOffset CheckedAt,
    IReadOnlyDictionary<string, string> Checks);
