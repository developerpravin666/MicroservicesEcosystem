using FluentValidation;
using BuildingBlocks.Observability.Middleware;
using AccountingInventory.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.ExceptionHandling;

/// <summary>
/// .NET 10 IExceptionHandler implementation that converts domain, application, and
/// FluentValidation exceptions raised anywhere in the request pipeline into RFC 7807
/// ProblemDetails responses. Feature/handler code never needs its own try/catch.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.HeaderName] as string
            ?? httpContext.TraceIdentifier;

        var (statusCode, title, errors) = Map(exception);

        logger.LogError(
            exception,
            "Unhandled exception. CorrelationId={CorrelationId} StatusCode={StatusCode} Path={Path}",
            correlationId, statusCode, httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.io/{statusCode}",
            Instance = httpContext.Request.Path,
            Detail = environment.IsDevelopment() ? exception.Message : title,
        };

        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title, IReadOnlyDictionary<string, string[]>? Errors) Map(Exception exception)
        => exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),

            NotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Message, null),
            ConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message, null),
            UnauthorizedException unauthorized => (StatusCodes.Status401Unauthorized, unauthorized.Message, null),

            _ => (StatusCodes.Status500InternalServerError,
                  "An unexpected error occurred while processing your request.", null),
        };
}
