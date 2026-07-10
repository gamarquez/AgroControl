namespace AgroControl.Application.Fiscal;

public interface IArcaFiscalService
{
    Task<FiscalProbeResult> ProbeAsync(FiscalSettingsRecord settings, CancellationToken cancellationToken);
}
