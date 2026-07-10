using AgroControl.Application.Auth;

namespace AgroControl.Application.Sales;

internal sealed class SalesService(
    ISalesRepository salesRepository,
    IAuditLogRepository auditLogRepository) : ISalesService
{
    public Task<PosProductListResult> ListPosProductsAsync(IdentityContext identity, PosProductListQuery query, CancellationToken cancellationToken)
        => salesRepository.ListPosProductsAsync(identity.OrganizationId, SalesValidation.ValidateListQuery(query), cancellationToken);

    public Task<SaleListResult> ListSalesAsync(IdentityContext identity, SaleListQuery query, CancellationToken cancellationToken)
        => salesRepository.ListSalesAsync(
            identity.OrganizationId,
            SalesValidation.ValidateSaleListQuery(query),
            cancellationToken);

    public async Task<SaleRecord> GetSaleAsync(IdentityContext identity, Guid saleId, CancellationToken cancellationToken)
    {
        return await salesRepository.GetSaleAsync(
            identity.OrganizationId,
            SalesValidation.ValidateRequiredGuid(saleId, "venta"),
            cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");
    }

    public async Task<SaleRecord> CreateCashSaleAsync(IdentityContext identity, CreateCashSaleCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = new CreateCashSaleCommand(
            SalesValidation.ValidateRequiredGuid(command.CashSessionId, "sesion de caja"),
            SalesValidation.ValidateItems(command.Items),
            SalesValidation.NormalizeOptional(command.Notes));

        var sale = await salesRepository.CreateCashSaleAsync(identity.OrganizationId, identity.UserId, normalized, cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "sales",
            sale.SaleId.ToString(),
            "cash_sale_created",
            new Dictionary<string, object?>
            {
                ["ticketNumber"] = sale.TicketNumber,
                ["cashSessionId"] = sale.CashSessionId,
                ["totalAmount"] = sale.TotalAmount,
                ["itemCount"] = sale.Items.Count
            },
            cancellationToken);

        return sale;
    }

    public async Task<SaleRecord> CreateAccountSaleAsync(IdentityContext identity, CreateAccountSaleCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = new CreateAccountSaleCommand(
            SalesValidation.ValidateRequiredGuid(command.CustomerId, "cliente"),
            SalesValidation.ValidateItems(command.Items),
            SalesValidation.ValidateOptionalDueDate(command.DueDate),
            SalesValidation.NormalizeOptional(command.Notes));

        var sale = await salesRepository.CreateAccountSaleAsync(identity.OrganizationId, identity.UserId, normalized, cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "sales",
            sale.SaleId.ToString(),
            "account_sale_created",
            new Dictionary<string, object?>
            {
                ["ticketNumber"] = sale.TicketNumber,
                ["customerId"] = sale.CustomerId,
                ["totalAmount"] = sale.TotalAmount,
                ["itemCount"] = sale.Items.Count
            },
            cancellationToken);

        return sale;
    }

    public async Task<SaleRecord> CreateCheckoutSaleAsync(IdentityContext identity, CreateCheckoutSaleCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = new CreateCheckoutSaleCommand(
            command.CashSessionId is { } cashSessionId
                ? SalesValidation.ValidateRequiredGuid(cashSessionId, "sesion de caja")
                : null,
            command.CustomerId is { } customerId
                ? SalesValidation.ValidateRequiredGuid(customerId, "cliente")
                : null,
            SalesValidation.ValidateItems(command.Items),
            SalesValidation.ValidatePayments(command.Payments),
            SalesValidation.ValidateOptionalDueDate(command.DueDate),
            SalesValidation.NormalizeOptional(command.Notes));

        var sale = await salesRepository.CreateCheckoutSaleAsync(identity.OrganizationId, identity.UserId, normalized, cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "sales",
            sale.SaleId.ToString(),
            "checkout_sale_created",
            new Dictionary<string, object?>
            {
                ["ticketNumber"] = sale.TicketNumber,
                ["cashSessionId"] = sale.CashSessionId,
                ["customerId"] = sale.CustomerId,
                ["totalAmount"] = sale.TotalAmount,
                ["paidAmount"] = sale.PaidAmount,
                ["accountBalanceAmount"] = sale.AccountBalanceAmount,
                ["paymentMethods"] = sale.Payments.Select(payment => payment.PaymentMethod).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            },
            cancellationToken);

        return sale;
    }

    public async Task<SaleRecord> ReverseCashSaleAsync(IdentityContext identity, Guid saleId, ReverseCashSaleCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var currentSale = await salesRepository.GetSaleAsync(
            identity.OrganizationId,
            SalesValidation.ValidateRequiredGuid(saleId, "venta"),
            cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");

        _ = SalesValidation.ValidateReversalStatus(currentSale.Status);

        var reversedSale = await salesRepository.ReverseCashSaleAsync(
            identity.OrganizationId,
            identity.UserId,
            saleId,
            new ReverseCashSaleCommand(
                SalesValidation.ValidateRequiredGuid(command.CashSessionId, "sesion de caja"),
                SalesValidation.NormalizeOptional(command.ReversalNotes)),
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "sales",
            reversedSale.SaleId.ToString(),
            "cash_sale_reversed",
            new Dictionary<string, object?>
            {
                ["ticketNumber"] = reversedSale.TicketNumber,
                ["cashSessionId"] = reversedSale.CashSessionId,
                ["reversalCashSessionId"] = reversedSale.ReversalCashSessionId,
                ["totalAmount"] = reversedSale.TotalAmount
            },
            cancellationToken);

        return reversedSale;
    }

    public async Task<SaleRecord> ReturnSaleAsync(IdentityContext identity, Guid saleId, ReturnSaleCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var currentSale = await salesRepository.GetSaleAsync(
            identity.OrganizationId,
            SalesValidation.ValidateRequiredGuid(saleId, "venta"),
            cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");

        _ = SalesValidation.ValidateReturnStatus(currentSale.Status);

        var updatedSale = await salesRepository.ReturnSaleAsync(
            identity.OrganizationId,
            identity.UserId,
            saleId,
            new ReturnSaleCommand(
                command.CashSessionId is { } cashSessionId
                    ? SalesValidation.ValidateRequiredGuid(cashSessionId, "sesion de caja")
                    : null,
                SalesValidation.ValidateReturnItems(command.Items),
                SalesValidation.NormalizeOptional(command.Notes)),
            cancellationToken);

        var lastReturn = updatedSale.Returns.OrderByDescending(item => item.CreatedAt).FirstOrDefault();

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "sales",
            updatedSale.SaleId.ToString(),
            "sale_items_returned",
            new Dictionary<string, object?>
            {
                ["ticketNumber"] = updatedSale.TicketNumber,
                ["status"] = updatedSale.Status,
                ["returnTotalAmount"] = lastReturn?.ReturnTotalAmount,
                ["refundedPaidAmount"] = lastReturn?.RefundedPaidAmount,
                ["creditedAccountAmount"] = lastReturn?.CreditedAccountAmount,
                ["itemCount"] = lastReturn?.Items.Count ?? 0
            },
            cancellationToken);

        return updatedSale;
    }

    private static void EnsureWriter(IdentityContext identity)
    {
        if (!identity.Roles.Contains("administrator", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("manager", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("seller", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("cashier", StringComparer.OrdinalIgnoreCase))
        {
            throw new AuthorizationException("No cuenta con permisos para registrar ventas.");
        }
    }
}
