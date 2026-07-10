using AgroControl.Application.Auth;

namespace AgroControl.Application.Fiscal;

internal static class FiscalValidation
{
    private static readonly HashSet<string> AllowedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "disabled",
        "arca_wsfev1"
    };

    private static readonly HashSet<string> AllowedEnvironments = new(StringComparer.OrdinalIgnoreCase)
    {
        "homologation",
        "production"
    };

    private static readonly HashSet<string> AllowedDocumentKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "invoice",
        "credit_note"
    };

    public static FiscalSettingsRecord NormalizeSettings(FiscalSettingsRecord settings)
    {
        var provider = NormalizeRequired(settings.Provider, "proveedor fiscal");
        if (!AllowedProviders.Contains(provider))
        {
            throw new ValidationException("El proveedor fiscal informado no es valido.");
        }

        var environment = NormalizeRequired(settings.Environment, "entorno fiscal");
        if (!AllowedEnvironments.Contains(environment))
        {
            throw new ValidationException("El entorno fiscal informado no es valido.");
        }

        var taxpayerId = NormalizeRequired(settings.TaxpayerId, "CUIT fiscal");
        if (taxpayerId.Length < 11 || taxpayerId.Length > 13)
        {
            throw new ValidationException("El CUIT fiscal debe tener un formato valido.");
        }

        if (settings.PointOfSale is <= 0 or > 99998)
        {
            throw new ValidationException("El punto de venta fiscal debe estar entre 1 y 99998.");
        }

        return settings with
        {
            Provider = provider.ToLowerInvariant(),
            Environment = environment.ToLowerInvariant(),
            TaxpayerId = taxpayerId,
            ServiceName = NormalizeRequired(settings.ServiceName, "servicio fiscal").ToLowerInvariant(),
            DefaultDocumentType = NormalizeRequired(settings.DefaultDocumentType, "tipo de comprobante por defecto").ToLowerInvariant()
        };
    }

    public static int ValidateLimit(int limit)
        => limit switch
        {
            <= 0 => 20,
            > 100 => 100,
            _ => limit
        };

    public static string ValidateDocumentKind(string value)
    {
        var normalized = NormalizeRequired(value, "tipo de documento fiscal").ToLowerInvariant();
        if (!AllowedDocumentKinds.Contains(normalized))
        {
            throw new ValidationException("El tipo de documento fiscal informado no es valido.");
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException($"El {fieldName} es obligatorio.");
        }

        return normalized;
    }
}
