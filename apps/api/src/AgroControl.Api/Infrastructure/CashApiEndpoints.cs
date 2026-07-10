using AgroControl.Application.Auth;
using AgroControl.Application.Cash;
using AgroControl.Contracts.Cash;
using System.Security.Claims;

namespace AgroControl.Api.Infrastructure;

internal static class CashApiEndpoints
{
    public static IEndpointRouteBuilder MapCashApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/cash").RequireAuthorization();

        api.MapGet(
                "/overview",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICashService cashService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var overview = await cashService.GetOverviewAsync(identity, cancellationToken);
                    return TypedResults.Ok(ToOverviewResponse(overview));
                })
            .WithName("GetCashOverview");

        api.MapGet(
                "/movements",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICashService cashService,
                    int? page,
                    int? pageSize,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await cashService.ListMovementsAsync(identity, page ?? 1, pageSize ?? 20, cancellationToken);
                    return TypedResults.Ok(new CashMovementListResponse(
                        result.Items.Select(ToMovementResponse).ToArray(),
                        result.Page,
                        result.PageSize,
                        result.Total));
                })
            .WithName("ListCashMovements");

        api.MapPost(
                "/sessions/open",
                async (
                    OpenCashSessionRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICashService cashService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var session = await cashService.OpenSessionAsync(
                        identity,
                        new OpenCashSessionCommand(request.CashRegisterCode, request.OpeningAmount, request.OpeningNotes),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/cash/sessions/{session.CashSessionId}", ToSessionResponse(session));
                })
            .WithName("OpenCashSession");

        api.MapPost(
                "/movements",
                async (
                    CreateCashMovementRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICashService cashService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var movement = await cashService.CreateMovementAsync(
                        identity,
                        new CreateCashMovementCommand(
                            request.CashSessionId,
                            request.MovementType,
                            request.CategoryCode,
                            request.Concept,
                            request.PaymentMethod,
                            request.Amount,
                            request.ReferenceDocument,
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/cash/movements/{movement.CashMovementId}", ToMovementResponse(movement));
                })
            .WithName("CreateCashMovement");

        api.MapPost(
                "/sessions/{cashSessionId:guid}/close",
                async (
                    Guid cashSessionId,
                    CloseCashSessionRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    ICashService cashService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var session = await cashService.CloseSessionAsync(
                        identity,
                        cashSessionId,
                        new CloseCashSessionCommand(request.ClosingAmount, request.ClosingNotes),
                        cancellationToken);

                    return TypedResults.Ok(ToSessionResponse(session));
                })
            .WithName("CloseCashSession");

        return app;
    }

    private static CashOverviewResponse ToOverviewResponse(CashOverviewRecord overview)
        => new(
            ToRegisterResponse(overview.CashRegister),
            overview.CurrentSession is null ? null : ToSessionResponse(overview.CurrentSession),
            overview.RecentMovements.Select(ToMovementResponse).ToArray());

    private static CashSessionResponse ToSessionResponse(CashSessionRecord session)
        => new(
            session.CashSessionId,
            ToRegisterResponse(session.CashRegister),
            session.OpeningAmount,
            session.ClosingAmount,
            session.DifferenceAmount,
            session.CurrentBalance,
            session.Status,
            session.OpeningNotes,
            session.ClosingNotes,
            session.OpenedByUserId,
            session.ClosedByUserId,
            session.OpenedAt,
            session.ClosedAt);

    private static CashRegisterResponse ToRegisterResponse(CashRegisterRecord register)
        => new(register.CashRegisterId, register.Name, register.Code, register.IsActive);

    private static CashMovementResponse ToMovementResponse(CashMovementRecord movement)
        => new(
            movement.CashMovementId,
            movement.CashSessionId,
            movement.MovementType,
            movement.CategoryCode,
            movement.Concept,
            movement.PaymentMethod,
            movement.Amount,
            movement.SignedAmount,
            movement.ResultingBalance,
            movement.ReferenceDocument,
            movement.Notes,
            movement.PerformedByUserId,
            movement.CreatedAt);
}
