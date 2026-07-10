namespace AgroControl.Application.Diagnostics;

public sealed record DatabaseProbeResult(
    bool IsHealthy,
    string Description,
    TimeSpan Duration);
