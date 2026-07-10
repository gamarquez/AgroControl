using AgroControl.Api.Infrastructure;
using AgroControl.Application;
using AgroControl.Application.Auth;
using AgroControl.Application.Diagnostics;
using AgroControl.Contracts.Health;
using AgroControl.Infrastructure;
using AgroControl.Infrastructure.Auth;
using AgroControl.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var isProduction = builder.Environment.IsProduction();
var authSettings = DeploymentConfiguration.CreateAuthSettings(builder.Configuration, isProduction);
var allowedCorsOrigins = DeploymentConfiguration.GetAllowedCorsOrigins(builder.Configuration, isProduction);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;

        if (context.ProblemDetails.Status is StatusCodes.Status500InternalServerError)
        {
            context.ProblemDetails.Title ??= "Se produjo un error inesperado.";
        }
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, isProduction);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = authSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(authSettings.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = authSettings.ClockSkew
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth-login", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("auth-refresh", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AgroControlWebClient", policy =>
    {
        policy
            .WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseForwardedHeaders();
app.UseRateLimiter();
app.UseStatusCodePages(async statusCodeContext =>
{
    var problemDetailsService = statusCodeContext.HttpContext.RequestServices
        .GetRequiredService<IProblemDetailsService>();

    await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
    {
        HttpContext = statusCodeContext.HttpContext,
        ProblemDetails = new ProblemDetails
        {
            Status = statusCodeContext.HttpContext.Response.StatusCode,
            Title = "La solicitud no pudo procesarse."
        }
    });
});
app.UseCors("AgroControlWebClient");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", (IHostEnvironment environment) =>
{
    var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    return TypedResults.Ok(new ApiHealthResponse(
        Status: "healthy",
        Service: "AgroControl.Api",
        Version: version,
        Environment: environment.EnvironmentName,
        CheckedAt: DateTimeOffset.UtcNow,
        Checks: new Dictionary<string, string>
        {
            ["api"] = "healthy"
        }));
})
.WithName("GetApiHealth");

app.MapGet(
    "/health/ready",
    async (
        IHostEnvironment environment,
        IApiReadinessService readinessService,
        CancellationToken cancellationToken) =>
    {
        var snapshot = await readinessService.GetSnapshotAsync(
            environment.EnvironmentName,
            cancellationToken);

        var response = new ApiReadinessResponse(
            Status: snapshot.IsHealthy ? "healthy" : "unhealthy",
            Service: "AgroControl.Api",
            Environment: snapshot.Environment,
            CheckedAt: snapshot.CheckedAt,
            Dependencies: snapshot.Dependencies
                .Select(dependency => new ApiDependencyHealthResponse(
                    dependency.Name,
                    dependency.IsHealthy ? "healthy" : "unhealthy",
                    dependency.Description,
                    Convert.ToInt64(Math.Ceiling(dependency.Duration.TotalMilliseconds))))
                .ToArray());

        return snapshot.IsHealthy
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    })
.WithName("GetApiReadiness");

app.MapAuthApi();
app.MapCatalogApi();
app.MapStockApi();
app.MapCashApi();
app.MapCustomersApi();
app.MapSalesApi();
app.MapFiscalApi();

app.Run();

public partial class Program;
