using AgroControl.Application.Auth;

namespace AgroControl.Application.Customers;

internal static class CustomerValidation
{
    public static CustomerListQuery ValidateListQuery(CustomerListQuery query)
        => query with
        {
            Search = NormalizeOptional(query.Search),
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
            throw new ValidationException($"El {fieldName} es obligatorio.");
        }

        return value;
    }

    public static string ValidateDisplayName(string displayName)
    {
        var normalized = NormalizeOptional(displayName);
        if (normalized is null || normalized.Length < 3)
        {
            throw new ValidationException("El nombre del cliente debe tener al menos 3 caracteres.");
        }

        return normalized;
    }

    public static decimal ValidateCreditLimit(decimal creditLimitAmount)
    {
        if (creditLimitAmount < 0)
        {
            throw new ValidationException("El limite de credito no puede ser negativo.");
        }

        return creditLimitAmount;
    }

    public static decimal ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ValidationException("El importe debe ser mayor a cero.");
        }

        return amount;
    }

    public static string ValidateConcept(string concept)
    {
        var normalized = NormalizeOptional(concept);
        if (normalized is null || normalized.Length < 3)
        {
            throw new ValidationException("El concepto debe tener al menos 3 caracteres.");
        }

        return normalized;
    }

    public static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
