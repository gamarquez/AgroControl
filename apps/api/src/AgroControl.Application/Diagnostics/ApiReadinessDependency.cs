namespace AgroControl.Application.Diagnostics;

public sealed record ApiReadinessDependency(
    string Name,
    bool IsHealthy,
    string Description,
    TimeSpan Duration);
