using AgroControl.Application.Auth;
using AgroControl.Contracts.Auth;
using AgroControl.Contracts.Settings;
using AgroControl.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AgroControl.Api.Infrastructure;

internal static class AuthApiEndpoints
{
    public static IEndpointRouteBuilder MapAuthApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapPost(
                "/auth/login",
                async (LoginRequest request, IAuthService authService, CancellationToken cancellationToken) =>
                {
                    var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
                    return TypedResults.Ok(ToSessionResponse(result));
                })
            .WithName("Login")
            .RequireRateLimiting("auth-login");

        api.MapPost(
                "/auth/refresh",
                async (RefreshRequest request, IAuthService authService, CancellationToken cancellationToken) =>
                {
                    var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
                    return TypedResults.Ok(ToSessionResponse(result));
                })
            .WithName("RefreshToken")
            .RequireRateLimiting("auth-refresh");

        api.MapPost(
                "/auth/logout",
                async (
                    LogoutRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IAuthService authService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    await authService.LogoutAsync(identity, request.RefreshToken, cancellationToken);
                    return TypedResults.NoContent();
                })
            .WithName("Logout")
            .RequireAuthorization();

        api.MapGet(
                "/auth/me",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IAuthService authService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var user = await authService.GetCurrentUserAsync(identity, cancellationToken)
                               ?? throw new NotFoundException("No se encontro la sesion actual.");

                    return TypedResults.Ok(ToUserResponse(user));
                })
            .WithName("GetCurrentSession")
            .RequireAuthorization();

        api.MapGet(
                "/users",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IUserManagementService userManagementService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var users = await userManagementService.ListUsersAsync(identity, cancellationToken);
                    return TypedResults.Ok(new UserListResponse(users.Select(ToUserResponse).ToArray()));
                })
            .WithName("ListUsers")
            .RequireAuthorization();

        api.MapGet(
                "/users/roles",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IUserManagementService userManagementService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var roles = await userManagementService.ListRolesAsync(identity, cancellationToken);
                    return TypedResults.Ok(new RoleListResponse(roles.Select(ToRoleResponse).ToArray()));
                })
            .WithName("ListRoles")
            .RequireAuthorization();

        api.MapPost(
                "/users",
                async (
                    CreateUserRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IUserManagementService userManagementService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var user = await userManagementService.CreateUserAsync(
                        identity,
                        new CreateUserCommand(
                            request.Email,
                            request.DisplayName,
                            request.Password,
                            request.MustChangePassword,
                            request.RoleIds),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/users/{user.UserId}", ToUserResponse(user));
                })
            .WithName("CreateUser")
            .RequireAuthorization();

        api.MapPatch(
                "/users/{userId:guid}",
                async (
                    Guid userId,
                    UpdateUserRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IUserManagementService userManagementService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var user = await userManagementService.UpdateUserAsync(
                        identity,
                        userId,
                        new UpdateUserCommand(
                            request.DisplayName,
                            request.IsActive,
                            request.IsLocked,
                            request.MustChangePassword,
                            request.RoleIds),
                        cancellationToken);

                    return TypedResults.Ok(ToUserResponse(user));
                })
            .WithName("UpdateUser")
            .RequireAuthorization();

        api.MapGet(
                "/settings/organization",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IUserManagementService userManagementService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var settings = await userManagementService.GetOrganizationSettingsAsync(identity, cancellationToken);
                    return TypedResults.Ok(ToOrganizationSettingsResponse(settings));
                })
            .WithName("GetOrganizationSettings")
            .RequireAuthorization();

        api.MapPut(
                "/settings/organization",
                async (
                    UpdateOrganizationSettingsRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IUserManagementService userManagementService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var updated = await userManagementService.UpdateOrganizationSettingsAsync(
                        identity,
                        new OrganizationSettingsRecord(
                            identity.OrganizationId,
                            request.LegalName,
                            request.TradeName,
                            request.TaxId,
                            request.TimeZone,
                            request.CurrencyCode),
                        cancellationToken);

                    return TypedResults.Ok(ToOrganizationSettingsResponse(updated));
                })
            .WithName("UpdateOrganizationSettings")
            .RequireAuthorization();

        return app;
    }

    private static AuthSessionResponse ToSessionResponse(AuthResult result)
    {
        return new AuthSessionResponse(
            ToUserResponse(result.User),
            new AuthTokenResponse(
                result.Tokens.AccessToken,
                result.Tokens.AccessTokenExpiresAt,
                result.Tokens.RefreshToken,
                result.Tokens.RefreshTokenExpiresAt));
    }

    private static AuthUserResponse ToUserResponse(AuthenticatedUser user)
    {
        return new AuthUserResponse(
            user.UserId,
            user.OrganizationId,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.IsLocked,
            user.MustChangePassword,
            user.Roles.Select(ToRoleResponse).ToArray(),
            user.Permissions);
    }

    private static AuthRoleResponse ToRoleResponse(RoleAssignment role)
    {
        return new AuthRoleResponse(role.RoleId, role.Code, role.Name);
    }

    private static OrganizationSettingsResponse ToOrganizationSettingsResponse(OrganizationSettingsRecord settings)
    {
        return new OrganizationSettingsResponse(
            settings.OrganizationId,
            settings.LegalName,
            settings.TradeName,
            settings.TaxId,
            settings.TimeZone,
            settings.CurrencyCode);
    }
}
