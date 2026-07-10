namespace AgroControl.Contracts.Fiscal;

public sealed record FiscalProbeResponse(
    bool IsConfigured,
    bool IsReachable,
    string Provider,
    string Environment,
    string Summary,
    string? TokenExpiresAt,
    string? AuthServer,
    string? AppServer,
    string? DbServer);
