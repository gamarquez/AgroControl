using AgroControl.Application.Auth;
using AgroControl.Application.Sales;
using AgroControl.Contracts.Sales;
using System.Security.Claims;

namespace AgroControl.Api.Infrastructure;

internal static class SalesApiEndpoints
{
    public static IEndpointRouteBuilder MapSalesApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/pos").RequireAuthorization();

        api.MapGet(
                "/products",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ISalesService salesService,
                    string? search,
                    int? page,
                    int? pageSize,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await salesService.ListPosProductsAsync(
                        identity,
                        new PosProductListQuery(search, page ?? 1, pageSize ?? 24),
                        cancellationToken);

                    return TypedResults.Ok(new PosProductListResponse(
                        result.Items.Select(ToPosProductResponse).ToArray(),
                        result.Page,
                        result.PageSize,
                        result.Total));
                })
            .WithName("ListPosProducts");

        api.MapGet(
                "/sales",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ISalesService salesService,
                    string? search,
                    string? status,
                    Guid? customerId,
                    int? page,
                    int? pageSize,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await salesService.ListSalesAsync(
                        identity,
                        new SaleListQuery(search, status, customerId, page ?? 1, pageSize ?? 20),
                        cancellationToken);
                    return TypedResults.Ok(new SaleListResponse(
                        result.Items.Select(ToSaleSummaryResponse).ToArray(),
                        result.Page,
                        result.PageSize,
                        result.Total));
                })
            .WithName("ListSales");

        api.MapGet(
                "/sales/{saleId:guid}",
                async (
                    Guid saleId,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ISalesService salesService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var sale = await salesService.GetSaleAsync(identity, saleId, cancellationToken);
                    return TypedResults.Ok(ToSaleResponse(sale));
                })
            .WithName("GetSale");

        api.MapPost(
                "/sales/cash",
                async (
                    CreateCashSaleRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ISalesService salesService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var sale = await salesService.CreateCashSaleAsync(
                        identity,
                        new CreateCashSaleCommand(
                            request.CashSessionId,
                            request.Items.Select(item => new CreateSaleItemCommand(item.ProductId, item.Quantity)).ToArray(),
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/pos/sales/{sale.SaleId}", ToSaleResponse(sale));
                })
            .WithName("CreateCashSale");

        api.MapPost(
                "/sales/account",
                async (
                    CreateAccountSaleRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ISalesService salesService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var sale = await salesService.CreateAccountSaleAsync(
                        identity,
                        new CreateAccountSaleCommand(
                            request.CustomerId,
                            request.Items.Select(item => new CreateSaleItemCommand(item.ProductId, item.Quantity)).ToArray(),
                            request.DueDate,
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/pos/sales/{sale.SaleId}", ToSaleResponse(sale));
                })
            .WithName("CreateAccountSale");

        api.MapPost(
                "/sales/checkout",
                async (
                    CreateCheckoutSaleRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ISalesService salesService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var sale = await salesService.CreateCheckoutSaleAsync(
                        identity,
                        new CreateCheckoutSaleCommand(
                            request.CashSessionId,
                            request.CustomerId,
                            request.Items.Select(item => new CreateSaleItemCommand(item.ProductId, item.Quantity)).ToArray(),
                            request.Payments.Select(payment => new CreateSalePaymentCommand(
                                payment.PaymentMethod,
                                payment.Amount,
                                payment.Reference,
                                payment.ProviderName)).ToArray(),
                            request.DueDate,
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/pos/sales/{sale.SaleId}", ToSaleResponse(sale));
                })
            .WithName("CreateCheckoutSale");

        api.MapPost(
                "/sales/{saleId:guid}/reverse",
                async (
                    Guid saleId,
                    ReverseCashSaleRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ISalesService salesService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var sale = await salesService.ReverseCashSaleAsync(
                        identity,
                        saleId,
                        new ReverseCashSaleCommand(request.CashSessionId, request.ReversalNotes),
                        cancellationToken);

                    return TypedResults.Ok(ToSaleResponse(sale));
                })
            .WithName("ReverseCashSale");

        api.MapPost(
                "/sales/{saleId:guid}/returns",
                async (
                    Guid saleId,
                    ReturnSaleRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ISalesService salesService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var sale = await salesService.ReturnSaleAsync(
                        identity,
                        saleId,
                        new ReturnSaleCommand(
                            request.CashSessionId,
                            request.Items.Select(item => new ReturnSaleItemCommand(item.SaleItemId, item.Quantity)).ToArray(),
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Ok(ToSaleResponse(sale));
                })
            .WithName("ReturnSale");

        return app;
    }

    private static PosProductResponse ToPosProductResponse(PosProductRecord product)
        => new(
            product.ProductId,
            product.Name,
            product.InternalCode,
            product.Sku,
            product.Barcode,
            product.AllowsFraction,
            product.UnitSymbol,
            product.OnHandQuantity,
            product.SaleAmount,
            product.CurrencyCode);

    private static SaleSummaryResponse ToSaleSummaryResponse(SaleSummaryRecord sale)
        => new(
            sale.SaleId,
            sale.TicketNumber,
            sale.CashSessionId,
            sale.CustomerId,
            sale.CustomerName,
            sale.SaleChannel,
            sale.Status,
            sale.TotalAmount,
            sale.PaidAmount,
            sale.AccountBalanceAmount,
            sale.CreditBalanceAppliedAmount,
            sale.DueDate,
            sale.CurrencyCode,
            sale.ItemCount,
            sale.CreatedAt);

    private static SaleResponse ToSaleResponse(SaleRecord sale)
        => new(
            sale.SaleId,
            sale.TicketNumber,
            sale.CashSessionId,
            sale.CustomerId,
            sale.SoldByUserId,
            sale.SaleChannel,
            sale.Status,
            sale.CustomerName,
            sale.SubtotalAmount,
            sale.DiscountAmount,
            sale.TotalAmount,
            sale.PaidAmount,
            sale.AccountBalanceAmount,
            sale.CreditBalanceAppliedAmount,
            sale.DueDate,
            sale.CurrencyCode,
            sale.ReversalCashSessionId,
            sale.ReversedByUserId,
            sale.ReversedAt,
            sale.Notes,
            sale.ReversalNotes,
            sale.CreatedAt,
            sale.Items.Select(ToSaleItemResponse).ToArray(),
            sale.Payments.Select(ToSalePaymentResponse).ToArray(),
            sale.Returns.Select(ToSaleReturnResponse).ToArray());

    private static SaleItemResponse ToSaleItemResponse(SaleItemRecord item)
        => new(
            item.SaleItemId,
            item.ProductId,
            item.ProductName,
            item.UnitSymbol,
            item.Quantity,
            item.ReturnedQuantity,
            item.AvailableToReturnQuantity,
            item.UnitPrice,
            item.LineTotal);

    private static SalePaymentResponse ToSalePaymentResponse(SalePaymentRecord payment)
        => new(
            payment.SalePaymentId,
            payment.PaymentMethod,
            payment.Amount,
            payment.Reference,
            payment.ProviderName,
            payment.CreatedAt);

    private static SaleReturnResponse ToSaleReturnResponse(SaleReturnRecord saleReturn)
        => new(
            saleReturn.SaleReturnId,
            saleReturn.SaleId,
            saleReturn.CashSessionId,
            saleReturn.CustomerAccountMovementId,
            saleReturn.ReturnedByUserId,
            saleReturn.ReturnTotalAmount,
            saleReturn.RefundedPaidAmount,
            saleReturn.CreditedAccountAmount,
            saleReturn.Notes,
            saleReturn.CreatedAt,
            saleReturn.Items.Select(ToSaleReturnItemResponse).ToArray());

    private static SaleReturnItemResponse ToSaleReturnItemResponse(SaleReturnItemRecord item)
        => new(
            item.SaleReturnItemId,
            item.SaleItemId,
            item.ProductId,
            item.Quantity,
            item.UnitPrice,
            item.LineTotal,
            item.CreatedAt);
}
