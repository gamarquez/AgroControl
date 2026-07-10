using AgroControl.Application.Auth;
using AgroControl.Application.Sales;
using System.Text.Json;

namespace AgroControl.Application.Fiscal;

internal sealed class FiscalService(
    IFiscalRepository fiscalRepository,
    ISalesRepository salesRepository,
    IArcaFiscalService arcaFiscalService,
    IAuditLogRepository auditLogRepository) : IFiscalService
{
    public async Task<FiscalSettingsRecord> GetSettingsAsync(IdentityContext identity, CancellationToken cancellationToken)
    {
        EnsureReader(identity);

        return await fiscalRepository.GetSettingsAsync(identity.OrganizationId, cancellationToken)
            ?? new FiscalSettingsRecord(
                identity.OrganizationId,
                "disabled",
                "homologation",
                string.Empty,
                1,
                "wsfe",
                "invoice_c",
                false,
                DateTimeOffset.UtcNow);
    }

    public async Task<FiscalSettingsRecord> UpdateSettingsAsync(IdentityContext identity, FiscalSettingsRecord settings, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = FiscalValidation.NormalizeSettings(settings with
        {
            OrganizationId = identity.OrganizationId,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var updated = await fiscalRepository.UpsertSettingsAsync(normalized, cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "fiscal_settings",
            identity.OrganizationId.ToString(),
            "fiscal_settings_updated",
            new Dictionary<string, object?>
            {
                ["provider"] = updated.Provider,
                ["environment"] = updated.Environment,
                ["pointOfSale"] = updated.PointOfSale,
                ["serviceName"] = updated.ServiceName,
                ["isEnabled"] = updated.IsEnabled
            },
            cancellationToken);

        return updated;
    }

    public async Task<FiscalDocumentListResult> ListDocumentsAsync(IdentityContext identity, int limit, CancellationToken cancellationToken)
    {
        EnsureReader(identity);
        return await fiscalRepository.ListDocumentsAsync(identity.OrganizationId, FiscalValidation.ValidateLimit(limit), cancellationToken);
    }

    public async Task<FiscalDocumentRecord> CreateFiscalDocumentAsync(IdentityContext identity, CreateFiscalDocumentCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var settings = await fiscalRepository.GetSettingsAsync(identity.OrganizationId, cancellationToken)
            ?? throw new ValidationException("Debes configurar ARCA antes de preparar documentos fiscales.");

        var documentKind = FiscalValidation.ValidateDocumentKind(command.DocumentKind);
        var existing = await fiscalRepository.GetDocumentBySaleAsync(identity.OrganizationId, command.SaleId, documentKind, cancellationToken);
        if (existing is not null)
        {
            throw new ValidationException("La venta ya tiene un documento fiscal preparado para ese tipo.");
        }

        var sale = await salesRepository.GetSaleAsync(identity.OrganizationId, command.SaleId, cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");

        if (!string.Equals(sale.Status, "confirmed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Solo puedes preparar documentos fiscales para ventas confirmadas.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            saleId = sale.SaleId,
            sale.TicketNumber,
            sale.CustomerId,
            sale.CustomerName,
            sale.TotalAmount,
            sale.CurrencyCode,
            documentKind,
            settings.Provider,
            settings.Environment,
            settings.ServiceName,
            settings.TaxpayerId,
            settings.PointOfSale
        });

        var lastError = settings.IsEnabled && string.Equals(settings.Provider, "arca_wsfev1", StringComparison.OrdinalIgnoreCase)
            ? "Documento preparado. La autorización FECAESolicitar queda pendiente hasta cerrar la parametrización fiscal final."
            : "Integración fiscal deshabilitada o pendiente de configuración.";

        var created = await fiscalRepository.CreatePendingDocumentAsync(
            identity.OrganizationId,
            new SaleDocumentSnapshot(
                sale.SaleId,
                sale.TicketNumber,
                sale.CustomerId,
                sale.CustomerName,
                sale.TotalAmount,
                sale.CurrencyCode,
                sale.Status),
            settings,
            documentKind,
            lastError,
            payload,
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "fiscal_documents",
            created.FiscalDocumentId.ToString(),
            "fiscal_document_queued",
            new Dictionary<string, object?>
            {
                ["saleId"] = created.SaleId,
                ["ticketNumber"] = created.TicketNumber,
                ["documentKind"] = created.DocumentKind,
                ["provider"] = created.Provider,
                ["environment"] = created.Environment
            },
            cancellationToken);

        return created;
    }

    public async Task<FiscalProbeResult> ProbeAsync(IdentityContext identity, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var settings = await fiscalRepository.GetSettingsAsync(identity.OrganizationId, cancellationToken)
            ?? throw new ValidationException("Debes configurar ARCA antes de ejecutar la prueba.");

        var probe = await arcaFiscalService.ProbeAsync(settings, cancellationToken);

        await fiscalRepository.WriteRequestLogAsync(
            identity.OrganizationId,
            null,
            "probe",
            JsonSerializer.Serialize(new
            {
                settings.Provider,
                settings.Environment,
                settings.ServiceName,
                settings.PointOfSale
            }),
            JsonSerializer.Serialize(probe),
            probe.IsReachable,
            probe.IsReachable ? null : probe.Summary,
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "fiscal_probe",
            identity.OrganizationId.ToString(),
            "fiscal_probe_executed",
            new Dictionary<string, object?>
            {
                ["provider"] = probe.Provider,
                ["environment"] = probe.Environment,
                ["isReachable"] = probe.IsReachable,
                ["summary"] = probe.Summary
            },
            cancellationToken);

        return probe;
    }

    private static void EnsureReader(IdentityContext identity)
    {
        if (!identity.Roles.Contains("administrator", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("manager", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("seller", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("cashier", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("viewer", StringComparer.OrdinalIgnoreCase))
        {
            throw new AuthorizationException("No cuenta con permisos para consultar ARCA.");
        }
    }

    private static void EnsureWriter(IdentityContext identity)
    {
        if (!identity.Roles.Contains("administrator", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("manager", StringComparer.OrdinalIgnoreCase))
        {
            throw new AuthorizationException("No cuenta con permisos para operar la configuración fiscal.");
        }
    }
}
