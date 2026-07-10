using AgroControl.Application.Auth;

namespace AgroControl.Application.Cash;

internal static class CashValidation
{
    private static readonly HashSet<string> AllowedMovementTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "cash_in",
        "cash_out"
    };

    private static readonly HashSet<string> AllowedPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "cash",
        "transfer",
        "qr",
        "card",
        "account"
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

    public static decimal ValidateNonNegativeAmount(decimal amount, string fieldName)
    {
        if (amount < 0)
        {
            throw new ValidationException($"El {fieldName} no puede ser negativo.");
        }

        return amount;
    }

    public static decimal ValidatePositiveAmount(decimal amount, string fieldName)
    {
        if (amount <= 0)
        {
            throw new ValidationException($"El {fieldName} debe ser mayor a cero.");
        }

        return amount;
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

    public static string ValidatePaymentMethod(string paymentMethod)
    {
        var normalized = NormalizeRequired(paymentMethod, "medio de pago");
        if (!AllowedPaymentMethods.Contains(normalized))
        {
            throw new ValidationException("El medio de pago informado no es valido.");
        }

        return normalized;
    }

    public static string ValidateCashRegisterCode(string cashRegisterCode)
        => NormalizeRequired(cashRegisterCode, "codigo de caja");

    public static string ValidateCategoryCode(string categoryCode)
        => NormalizeRequired(categoryCode, "categoria");

    public static string ValidateConcept(string concept)
        => NormalizeRequired(concept, "concepto");

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
}
