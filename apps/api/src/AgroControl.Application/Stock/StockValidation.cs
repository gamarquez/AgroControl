using AgroControl.Application.Auth;

namespace AgroControl.Application.Stock;

internal static class StockValidation
{
    private static readonly HashSet<string> AllowedMovementTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "purchase_inbound",
        "sale_outbound",
        "adjustment_increase",
        "adjustment_decrease",
        "return_inbound",
        "loss",
        "broken",
        "expired"
    };

    public static StockListQuery ValidateListQuery(StockListQuery query)
        => query with
        {
            Search = NormalizeOptional(query.Search),
            WarehouseId = query.WarehouseId is { } warehouseId ? ValidateRequiredGuid(warehouseId, "deposito") : null,
            Page = query.Page <= 0 ? 1 : query.Page,
            PageSize = query.PageSize switch
            {
                <= 0 => 20,
                > 100 => 100,
                _ => query.PageSize
            }
        };

    public static int ValidatePage(int page) => page <= 0 ? 1 : page;

    public static int ValidatePageSize(int pageSize)
        => pageSize switch
        {
            <= 0 => 20,
            > 100 => 100,
            _ => pageSize
        };

    public static Guid ValidateRequiredGuid(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException($"La {fieldName} es obligatoria.");
        }

        return value;
    }

    public static string ValidateMovementType(string movementType)
    {
        var normalized = NormalizeRequired(movementType, "tipo de movimiento");

        if (!AllowedMovementTypes.Contains(normalized))
        {
            throw new ValidationException("El tipo de movimiento informado no es valido.");
        }

        return normalized;
    }

    public static decimal ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ValidationException("La cantidad debe ser mayor a cero.");
        }

        return quantity;
    }

    public static decimal ValidateNonNegativeQuantity(decimal quantity, string fieldName)
    {
        if (quantity < 0)
        {
            throw new ValidationException($"La {fieldName} no puede ser negativa.");
        }

        return quantity;
    }

    public static decimal? ValidateOptionalQuantity(decimal? quantity, string fieldName)
    {
        if (quantity is null)
        {
            return null;
        }

        if (quantity < 0)
        {
            throw new ValidationException($"La {fieldName} no puede ser negativa.");
        }

        return quantity;
    }

    public static void ValidateStockPolicy(decimal? minQuantity, decimal? maxQuantity, decimal? reorderPoint)
    {
        if (minQuantity is not null && maxQuantity is not null && maxQuantity < minQuantity)
        {
            throw new ValidationException("El stock maximo no puede ser menor al stock minimo.");
        }

        if (reorderPoint is not null && minQuantity is not null && reorderPoint < minQuantity)
        {
            throw new ValidationException("El punto de reposicion no puede ser menor al stock minimo.");
        }
    }

    public static string ValidateReason(string reason)
        => NormalizeRequired(reason, "motivo");

    public static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            throw new ValidationException($"El {fieldName} es obligatorio.");
        }

        return normalized;
    }

    public static string ValidateWarehouseName(string name)
    {
        var normalized = NormalizeRequired(name, "nombre del deposito");
        if (normalized.Length < 3)
        {
            throw new ValidationException("El nombre del deposito debe tener al menos 3 caracteres.");
        }

        return normalized;
    }

    public static string ValidateWarehouseCode(string code)
    {
        var normalized = NormalizeRequired(code, "codigo del deposito").ToUpperInvariant();
        if (normalized.Length < 2)
        {
            throw new ValidationException("El codigo del deposito debe tener al menos 2 caracteres.");
        }

        return normalized;
    }

    public static int ValidateLimit(int limit)
        => limit switch
        {
            <= 0 => 10,
            > 100 => 100,
            _ => limit
        };
}
