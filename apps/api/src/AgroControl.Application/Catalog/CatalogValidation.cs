using AgroControl.Application.Auth;

namespace AgroControl.Application.Catalog;

public static class CatalogValidation
{
    public static ProductListQuery ValidateListQuery(ProductListQuery query)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            <= 0 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        return query with
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = page,
            PageSize = pageSize
        };
    }

    public static string ValidateName(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"El campo {fieldName} es obligatorio.");
        }

        return value.Trim();
    }

    public static string ValidateCode(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"El campo {fieldName} es obligatorio.");
        }

        return value.Trim();
    }

    public static string ValidateCurrencyCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("La moneda es obligatoria.");
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length != 3)
        {
            throw new ValidationException("La moneda debe tener 3 caracteres.");
        }

        return normalized;
    }

    public static decimal ValidateAmount(decimal value, string fieldName)
    {
        if (value < 0)
        {
            throw new ValidationException($"El campo {fieldName} no puede ser negativo.");
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal? ValidateMargin(decimal? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 0)
        {
            throw new ValidationException("El margen no puede ser negativo.");
        }

        return decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero);
    }

    public static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static Guid ValidateRequiredGuid(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException($"El campo {fieldName} es obligatorio.");
        }

        return value;
    }
}
