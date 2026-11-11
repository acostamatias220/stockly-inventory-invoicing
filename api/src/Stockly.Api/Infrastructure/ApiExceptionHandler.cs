using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Stockly.Application.Common;
using Stockly.Domain.Common;

namespace Stockly.Api.Infrastructure;

/// <summary>Translates exceptions into RFC 7807 problem details with a stable shape for the client.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException ve => Validation(ve),
            NotFoundException => Problem(StatusCodes.Status404NotFound, "No encontrado", exception.Message),
            ConflictException => Problem(StatusCodes.Status409Conflict, "Conflicto", exception.Message),
            UnauthorizedException => Problem(StatusCodes.Status401Unauthorized, "No autorizado", exception.Message),
            ForbiddenException => Problem(StatusCodes.Status403Forbidden, "Acceso denegado", exception.Message),
            InsufficientStockException se => WithCode(Problem(StatusCodes.Status422UnprocessableEntity, "Stock insuficiente", se.Message), se.Code, new()
            {
                ["sku"] = se.Sku,
                ["requested"] = se.Requested,
                ["available"] = se.Available,
            }),
            DomainException de => WithCode(Problem(StatusCodes.Status422UnprocessableEntity, "Regla de negocio", de.Message), de.Code),
            DbUpdateException { InnerException: MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry } } =>
                Problem(StatusCodes.Status409Conflict, "Conflicto", "Ya existe un registro con esos datos."),
            BadHttpRequestException bad => Problem(bad.StatusCode, "Solicitud inválida", bad.Message),
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, "Error interno", "Ocurrió un error inesperado. Intentá nuevamente.");
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ProblemDetails WithCode(ProblemDetails problem, string code, Dictionary<string, object?>? extra = null)
    {
        problem.Extensions["code"] = code;
        foreach (var (key, value) in extra ?? [])
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }

    private static ValidationProblemDetails Validation(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => JsonName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Datos inválidos",
            Detail = "Revisá los campos marcados.",
        };
    }

    /// <summary>"Lines[0].Quantity" → "lines[0].quantity" so the client can map errors to form controls.</summary>
    internal static string JsonName(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}
