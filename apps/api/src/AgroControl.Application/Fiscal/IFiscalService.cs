using AgroControl.Application.Auth;

namespace AgroControl.Application.Fiscal;

public interface IFiscalService
{
    Task<FiscalSettingsRecord> GetSettingsAsync(IdentityContext identity, CancellationToken cancellationToken);

    Task<FiscalSettingsRecord> UpdateSettingsAsync(IdentityContext identity, FiscalSettingsRecord settings, CancellationToken cancellationToken);

    Task<FiscalDocumentListResult> ListDocumentsAsync(IdentityContext identity, int limit, CancellationToken cancellationToken);

    Task<FiscalDocumentRecord> CreateFiscalDocumentAsync(IdentityContext identity, CreateFiscalDocumentCommand command, CancellationToken cancellationToken);

    Task<FiscalProbeResult> ProbeAsync(IdentityContext identity, CancellationToken cancellationToken);
}
