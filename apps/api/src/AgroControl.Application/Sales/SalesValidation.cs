using AgroControl.Application.Auth;

namespace AgroControl.Application.Sales;

internal static class SalesValidation
{
    private static readonly HashSet<string> AllowedPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "cash",
        "transfer",
        "qr",
        "card",
        "account"
    };
    private static readonly HashSet<string> AllowedSaleStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "confirmed",
        "partially_returned",
        "fully_returned",
        "reversed"
    };

    public static PosProductListQuery ValidateListQuery(PosProductListQuery query)
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

    public static SaleListQuery ValidateSaleListQuery(SaleListQuery query)
        => query with
        {
            Search = NormalizeOptional(query.Search),
            Status = ValidateOptionalSaleStatus(query.Status),
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

    public static decimal ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ValidationException("La cantidad debe ser mayor a cero.");
        }

        return quantity;
    }

    public static IReadOnlyList<ReturnSaleItemCommand> ValidateReturnItems(IReadOnlyList<ReturnSaleItemCommand> items)
    {
        if (items.Count == 0)
        {
            throw new ValidationException("Debe informar al menos un item para la devolucion.");
        }

        return items
            .Select(item => new ReturnSaleItemCommand(
                ValidateRequiredGuid(item.SaleItemId, "item de venta"),
                ValidateQuantity(item.Quantity)))
            .ToArray();
    }

    public static IReadOnlyList<CreateSaleItemCommand> ValidateItems(IReadOnlyList<CreateSaleItemCommand> items)
    {
        if (items.Count == 0)
        {
            throw new ValidationException("Debe informar al menos un producto para la venta.");
        }

        return items
            .Select(item => new CreateSaleItemCommand(
                ValidateRequiredGuid(item.ProductId, "producto"),
                ValidateQuantity(item.Quantity)))
            .ToArray();
    }

    public static IReadOnlyList<CreateSalePaymentCommand> ValidatePayments(IReadOnlyList<CreateSalePaymentCommand> payments)
    {
        if (payments.Count == 0)
        {
            throw new ValidationException("Debe informar al menos un medio de pago.");
        }

        return payments
            .Select(payment => new CreateSalePaymentCommand(
                ValidatePaymentMethod(payment.PaymentMethod),
                ValidatePaymentAmount(payment.Amount),
                NormalizeOptional(payment.Reference),
                NormalizeOptional(payment.ProviderName)))
            .ToArray();
    }

    public static string ValidatePaymentMethod(string paymentMethod)
    {
        var normalized = NormalizeOptional(paymentMethod)
            ?? throw new ValidationException("El medio de pago es obligatorio.");

        if (!AllowedPaymentMethods.Contains(normalized))
        {
            throw new ValidationException("El medio de pago informado no es valido.");
        }

        return normalized.ToLowerInvariant();
    }

    public static decimal ValidatePaymentAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ValidationException("Cada importe de pago debe ser mayor a cero.");
        }

        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public static string ValidateReversalStatus(string status)
    {
        if (!string.Equals(status, "confirmed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Solo puedes revertir ventas confirmadas.");
        }

        return status;
    }

    public static string ValidateReturnStatus(string status)
    {
        if (!string.Equals(status, "confirmed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "partially_returned", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Solo puedes devolver items de ventas confirmadas o parcialmente devueltas.");
        }

        return status;
    }

    public static DateOnly? ValidateOptionalDueDate(DateOnly? dueDate)
    {
        if (dueDate is not null && dueDate.Value < DateOnly.FromDateTime(DateTime.UtcNow.Date))
        {
            throw new ValidationException("La fecha de vencimiento no puede estar en el pasado.");
        }

        return dueDate;
    }

    private static string? ValidateOptionalSaleStatus(string? status)
    {
        var normalized = NormalizeOptional(status);
        if (normalized is null)
        {
            return null;
        }

        if (!AllowedSaleStatuses.Contains(normalized))
        {
            throw new ValidationException("El estado de venta informado no es valido.");
        }

        return normalized.ToLowerInvariant();
    }
}
