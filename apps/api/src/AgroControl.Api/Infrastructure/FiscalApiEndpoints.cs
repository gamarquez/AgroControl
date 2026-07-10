using AgroControl.Application.Auth;
using AgroControl.Application.Fiscal;
using AgroControl.Contracts.Fiscal;
using System.Security.Claims;

namespace AgroControl.Api.Infrastructure;

internal static class FiscalApiEndpoints
{
    public static IEndpointRouteBuilder MapFiscalApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/fiscal").RequireAuthorization();

        api.MapGet(
                "/settings",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IFiscalService fiscalService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var settings = await fiscalService.GetSettingsAsync(identity, cancellationToken);
                    return TypedResults.Ok(ToSettingsResponse(settings));
                })
            .WithName("GetFiscalSettings");

        api.MapPut(
                "/settings",
                async (
                    UpdateFiscalSettingsRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IFiscalService fiscalService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var settings = await fiscalService.UpdateSettingsAsync(
                        identity,
                        new FiscalSettingsRecord(
                            identity.OrganizationId,
                            request.Provider,
                            request.Environment,
                            request.TaxpayerId,
                            request.PointOfSale,
                            request.ServiceName,
                            request.DefaultDocumentType,
                            request.IsEnabled,
                            DateTimeOffset.UtcNow),
                        cancellationToken);

                    return TypedResults.Ok(ToSettingsResponse(settings));
                })
            .WithName("UpdateFiscalSettings");

        api.MapGet(
                "/documents",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IFiscalService fiscalService,
                    int? limit,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await fiscalService.ListDocumentsAsync(identity, limit ?? 20, cancellationToken);
                    return TypedResults.Ok(new FiscalDocumentListResponse(
                        result.Items.Select(ToDocumentResponse).ToArray(),
                        result.Total));
                })
            .WithName("ListFiscalDocuments");

        api.MapPost(
                "/documents/sales/{saleId:guid}",
                async (
                    Guid saleId,
                    CreateFiscalDocumentRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IFiscalService fiscalService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var document = await fiscalService.CreateFiscalDocumentAsync(
                        identity,
                        new CreateFiscalDocumentCommand(saleId, request.DocumentKind),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/fiscal/documents/{document.FiscalDocumentId}", ToDocumentResponse(document));
                })
            .WithName("CreateFiscalDocument");

        api.MapPost(
                "/probe",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IFiscalService fiscalService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var probe = await fiscalService.ProbeAsync(identity, cancellationToken);
                    return TypedResults.Ok(new FiscalProbeResponse(
                        probe.IsConfigured,
                        probe.IsReachable,
                        probe.Provider,
                        probe.Environment,
                        probe.Summary,
                        probe.TokenExpiresAt,
                        probe.AuthServer,
                        probe.AppServer,
                        probe.DbServer));
                })
            .WithName("ProbeFiscalConnectivity");

        return app;
    }

    private static FiscalSettingsResponse ToSettingsResponse(FiscalSettingsRecord settings)
        => new(
            settings.OrganizationId,
            settings.Provider,
            settings.Environment,
            settings.TaxpayerId,
            settings.PointOfSale,
            settings.ServiceName,
            settings.DefaultDocumentType,
            settings.IsEnabled,
            settings.UpdatedAt);

    private static FiscalDocumentResponse ToDocumentResponse(FiscalDocumentRecord document)
        => new(
            document.FiscalDocumentId,
            document.SaleId,
            document.TicketNumber,
            document.CustomerId,
            document.CustomerName,
            document.TotalAmount,
            document.CurrencyCode,
            document.DocumentKind,
            document.Provider,
            document.Environment,
            document.ServiceName,
            document.TaxpayerId,
            document.PointOfSale,
            document.Status,
            document.DocumentNumber,
            document.Cae,
            document.CaeExpiresOn,
            document.ExternalReference,
            document.LastError,
            document.AttemptsCount,
            document.LastAttemptAt,
            document.CreatedAt,
            document.UpdatedAt);
}
