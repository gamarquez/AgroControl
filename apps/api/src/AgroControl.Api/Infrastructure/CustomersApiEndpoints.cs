using AgroControl.Application.Auth;
using AgroControl.Application.Customers;
using AgroControl.Contracts.Customers;
using System.Security.Claims;

namespace AgroControl.Api.Infrastructure;

internal static class CustomersApiEndpoints
{
    public static IEndpointRouteBuilder MapCustomersApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/customers").RequireAuthorization();

        api.MapGet(
                "",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICustomerService customerService,
                    string? search,
                    bool? isActive,
                    int? page,
                    int? pageSize,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await customerService.ListCustomersAsync(
                        identity,
                        new CustomerListQuery(search, isActive, page ?? 1, pageSize ?? 20),
                        cancellationToken);

                    return TypedResults.Ok(new CustomerListResponse(
                        result.Items.Select(ToCustomerResponse).ToArray(),
                        result.Page,
                        result.PageSize,
                        result.Total));
                })
            .WithName("ListCustomers");

        api.MapGet(
                "/{customerId:guid}",
                async (
                    Guid customerId,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICustomerService customerService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var customer = await customerService.GetCustomerAsync(identity, customerId, cancellationToken);
                    return TypedResults.Ok(ToCustomerResponse(customer));
                })
            .WithName("GetCustomer");

        api.MapPost(
                "",
                async (
                    CreateCustomerRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICustomerService customerService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var customer = await customerService.CreateCustomerAsync(
                        identity,
                        new CreateCustomerCommand(
                            request.DisplayName,
                            request.TaxId,
                            request.Phone,
                            request.Email,
                            request.Address,
                            request.CreditLimitAmount,
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/customers/{customer.CustomerId}", ToCustomerResponse(customer));
                })
            .WithName("CreateCustomer");

        api.MapPatch(
                "/{customerId:guid}",
                async (
                    Guid customerId,
                    UpdateCustomerRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICustomerService customerService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var customer = await customerService.UpdateCustomerAsync(
                        identity,
                        customerId,
                        new UpdateCustomerCommand(
                            request.DisplayName,
                            request.TaxId,
                            request.Phone,
                            request.Email,
                            request.Address,
                            request.CreditLimitAmount,
                            request.IsActive,
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Ok(ToCustomerResponse(customer));
                })
            .WithName("UpdateCustomer");

        api.MapGet(
                "/{customerId:guid}/statement",
                async (
                    Guid customerId,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICustomerService customerService,
                    int? limit,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var statement = await customerService.GetAccountStatementAsync(identity, customerId, limit ?? 50, cancellationToken);
                    return TypedResults.Ok(new CustomerAccountStatementResponse(
                        ToCustomerResponse(statement.Customer),
                        statement.Movements.Select(ToMovementResponse).ToArray()));
                })
            .WithName("GetCustomerAccountStatement");

        api.MapGet(
                "/{customerId:guid}/account-movements",
                async (
                    Guid customerId,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICustomerService customerService,
                    int? page,
                    int? pageSize,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await customerService.ListAccountMovementsAsync(
                        identity,
                        customerId,
                        page ?? 1,
                        pageSize ?? 20,
                        cancellationToken);

                    return TypedResults.Ok(new CustomerAccountMovementListResponse(
                        result.Items.Select(ToMovementResponse).ToArray(),
                        result.Page,
                        result.PageSize,
                        result.Total));
                })
            .WithName("ListCustomerAccountMovements");

        api.MapPost(
                "/{customerId:guid}/payments",
                async (
                    Guid customerId,
                    RecordCustomerPaymentRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICustomerService customerService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await customerService.RecordPaymentAsync(
                        identity,
                        customerId,
                        new RecordCustomerPaymentCommand(request.CashSessionId, request.Amount, request.Notes),
                        cancellationToken);

                    return TypedResults.Created(
                        $"/api/v1/customers/{customerId}/account-movements/{result.Movement.CustomerAccountMovementId}",
                        ToMovementResponse(result.Movement));
                })
            .WithName("RecordCustomerPayment");

        api.MapPost(
                "/{customerId:guid}/credit-notes",
                async (
                    Guid customerId,
                    RecordCustomerCreditNoteRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICustomerService customerService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await customerService.RecordCreditNoteAsync(
                        identity,
                        customerId,
                        new RecordCustomerCreditNoteCommand(
                            request.Amount,
                            request.Concept,
                            request.ReferenceDocument,
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Created(
                        $"/api/v1/customers/{customerId}/account-movements/{result.Movement.CustomerAccountMovementId}",
                        ToMovementResponse(result.Movement));
                })
            .WithName("RecordCustomerCreditNote");

        return app;
    }

    private static CustomerResponse ToCustomerResponse(CustomerRecord customer)
        => new(
            customer.CustomerId,
            customer.DisplayName,
            customer.TaxId,
            customer.Phone,
            customer.Email,
            customer.Address,
            customer.CreditLimitAmount,
            customer.CurrentBalance,
            customer.OverdueBalance,
            customer.NextDueDate,
            customer.IsActive,
            customer.Notes,
            customer.CreatedAt,
            customer.UpdatedAt);

    private static CustomerAccountMovementResponse ToMovementResponse(CustomerAccountMovementRecord movement)
        => new(
            movement.CustomerAccountMovementId,
            movement.CustomerId,
            movement.SaleId,
            movement.CashSessionId,
            movement.MovementType,
            movement.Concept,
            movement.ReferenceDocument,
            movement.DebitAmount,
            movement.CreditAmount,
            movement.OpenAmount,
            movement.IsOverdue,
            movement.DueDate,
            movement.ResultingBalance,
            movement.Notes,
            movement.PerformedByUserId,
            movement.CreatedAt);
}
