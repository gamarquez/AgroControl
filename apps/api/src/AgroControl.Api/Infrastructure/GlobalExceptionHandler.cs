using AgroControl.Application.Auth;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AgroControl.Api.Infrastructure;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail, level) = exception switch
        {
            ValidationException validationException => (StatusCodes.Status400BadRequest, "La solicitud no es valida.", validationException.Message, LogLevel.Warning),
            AuthenticationException authenticationException => (StatusCodes.Status401Unauthorized, "No fue posible autenticar la solicitud.", authenticationException.Message, LogLevel.Warning),
            AuthorizationException authorizationException => (StatusCodes.Status403Forbidden, "No cuenta con permisos para esta operacion.", authorizationException.Message, LogLevel.Warning),
            NotFoundException notFoundException => (StatusCodes.Status404NotFound, "No se encontro el recurso solicitado.", notFoundException.Message, LogLevel.Information),
            _ => (StatusCodes.Status500InternalServerError, "Se produjo un error inesperado.", "Revise los logs de la aplicacion para obtener mas informacion.", LogLevel.Error)
        };

        logger.Log(
            level,
            exception,
            "Request failure while processing {Method} {Path}.",
            httpContext.Request.Method,
            httpContext.Request.Path);

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            },
            Exception = exception
        });
    }
}
