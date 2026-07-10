namespace AgroControl.Application.Diagnostics;

public interface IAgroControlDatabaseProbe
{
    Task<DatabaseProbeResult> ProbeAsync(CancellationToken cancellationToken);
}
