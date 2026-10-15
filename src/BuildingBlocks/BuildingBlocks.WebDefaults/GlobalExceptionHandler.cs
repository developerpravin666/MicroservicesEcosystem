using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain;
using BuildingBlocks.Observability.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.WebDefaults;

/// <summary>
/// The one .NET 10 IExceptionHandler implementation for every service in the
/// ecosystem. Converts FluentValidation failures, any service's DomainException
/// subclass, and the shared Conflict/NotFound/Unauthorized/Forbidden exceptions into
/// standard success/message/data error responses. Because every service's domain exceptions
/// derive from the same BuildingBlocks.Domain.DomainException base, and every
/// service's Application-layer exceptions ARE the same shared types (from
/// BuildingBlocks.Application.Exceptions), this single handler covers Identity,
/// Inventory, Tenant, and any future Gold-Master clone without modification —
/// feature code anywhere never needs its own try/catch.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIdMiddleware.GetCurrentCorrelationId(httpContext) ?? httpContext.TraceIdentifier;

        var (statusCode, title, message, errors) = Map(exception);

        // Short and sweet server log message with actionable summary
        if (statusCode >= 500)
        {
            logger.LogError(
                exception,
                "[{StatusCode}] {Title} on {Method} {Path} (Trace:{CorrelationId}): {Message}",
                statusCode, title, httpContext.Request.Method, httpContext.Request.Path, message, correlationId);
        }
        else
        {
            logger.LogWarning(
                "[{StatusCode}] {Title} on {Method} {Path} (Trace:{CorrelationId}): {Message}",
                statusCode, title, httpContext.Request.Method, httpContext.Request.Path, message, correlationId);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var response = new ApiResponse<object?>(
            Success: false,
            Message: message,
            Data: null,
            Errors: errors,
            Metadata: new Dictionary<string, string>
            {
                ["title"] = title,
                ["correlationId"] = correlationId,
                ["traceId"] = httpContext.TraceIdentifier,
            });

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title, string Message, IReadOnlyDictionary<string, string[]>? Errors) Map(Exception exception)
    {
        var root = FindRootCause(exception);

        switch (root)
        {
            case ValidationException validationException:
                var validationErrors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                return (
                    StatusCodes.Status400BadRequest,
                    "Validation Error",
                    "Please verify and correct the highlighted fields.",
                    validationErrors);

            case DomainException domainException:
                return (StatusCodes.Status400BadRequest, "Business Rule Violation", domainException.Message, null);

            case BadRequestException badRequest:
                return (StatusCodes.Status400BadRequest, "Invalid Request", badRequest.Message, null);

            case NotFoundException notFound:
                return (StatusCodes.Status404NotFound, "Not Found", notFound.Message, null);

            case ConflictException conflict:
                return (StatusCodes.Status409Conflict, "Data Conflict", conflict.Message, null);

            case UnauthorizedException unauthorized:
                return (StatusCodes.Status401Unauthorized, "Authentication Required", "Session expired or invalid. Please sign in again.", null);

            case ForbiddenException forbidden:
                return (StatusCodes.Status403Forbidden, "Access Denied", forbidden.Message, null);

            case TimeoutException:
            case OperationCanceledException when !root.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase):
                return (StatusCodes.Status504GatewayTimeout, "Operation Timed Out", "The database or service took too long to respond. Please try again.", null);
        }

        // Check for PostgreSQL database exceptions via duck-typing
        if (root.GetType().Name == "PostgresException")
        {
            return DescribePostgresException(root);
        }

        if (root.GetType().Name.Contains("SocketException") || root.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase))
        {
            return (
                StatusCodes.Status503ServiceUnavailable,
                "Service Unreachable",
                "A required backend service is unreachable. Please verify network services are running.",
                null);
        }

        // Generic fallback: clean technical jargon for a short, understandable message
        var cleanSummary = CleanMessage(root.Message);
        return (
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            string.IsNullOrWhiteSpace(cleanSummary) ? "An unexpected error occurred while processing your request." : cleanSummary,
            null);
    }

    private static (int StatusCode, string Title, string Message, IReadOnlyDictionary<string, string[]>? Errors) DescribePostgresException(Exception ex)
    {
        var sqlState = ex.GetType().GetProperty("SqlState")?.GetValue(ex)?.ToString();
        var detail = ex.GetType().GetProperty("Detail")?.GetValue(ex)?.ToString();
        var colName = ex.GetType().GetProperty("ColumnName")?.GetValue(ex)?.ToString();
        var messageText = ex.GetType().GetProperty("MessageText")?.GetValue(ex)?.ToString() ?? ex.Message;

        switch (sqlState)
        {
            case "23505": // unique_violation
                var dup = !string.IsNullOrWhiteSpace(detail) ? detail : messageText;
                return (
                    StatusCodes.Status409Conflict,
                    "Duplicate Entry",
                    $"A record with this value already exists ({CleanMessage(dup)}). Please use a unique value.",
                    null);

            case "23503": // foreign_key_violation
                var fk = !string.IsNullOrWhiteSpace(detail) ? detail : messageText;
                return (
                    StatusCodes.Status400BadRequest,
                    "Missing Related Record",
                    $"Cannot complete operation because a required related record is missing ({CleanMessage(fk)}).",
                    null);

            case "23502": // not_null_violation
                var col = !string.IsNullOrWhiteSpace(colName) ? colName : "A required field";
                return (
                    StatusCodes.Status400BadRequest,
                    "Required Field Missing",
                    $"Field '{col}' cannot be blank. Please enter a valid value.",
                    null);

            case "42703": // undefined_column
                return (
                    StatusCodes.Status500InternalServerError,
                    "Database Schema Mismatch",
                    $"Database column '{CleanMessage(messageText)}' was not found. Please click 'Scan & Sync DB' on Table Management.",
                    null);

            case "42P01": // undefined_table
                return (
                    StatusCodes.Status500InternalServerError,
                    "Database Table Missing",
                    $"Database table '{CleanMessage(messageText)}' was not found. Please sync database tables.",
                    null);

            case "40001": // serialization_failure
                return (
                    StatusCodes.Status409Conflict,
                    "Database Busy",
                    "A temporary transaction conflict occurred. Please retry your request.",
                    null);

            case "28P01":
            case "28000":
                return (
                    StatusCodes.Status500InternalServerError,
                    "Database Auth Failed",
                    "Database access credentials were rejected. Please verify connection credentials.",
                    null);

            case "08001":
            case "08006":
                return (
                    StatusCodes.Status503ServiceUnavailable,
                    "Database Connection Failed",
                    "Unable to connect to the PostgreSQL database server. Please verify the database is running.",
                    null);

            default:
                return (
                    StatusCodes.Status500InternalServerError,
                    "Database Error",
                    CleanMessage(messageText),
                    null);
        }
    }

    private static Exception FindRootCause(Exception ex)
    {
        var current = ex;
        while (current.InnerException != null)
        {
            if (current is AggregateException agg && agg.InnerExceptions.Count == 1)
            {
                current = agg.InnerExceptions[0];
                continue;
            }

            var typeName = current.GetType().Name;
            if (typeName.Contains("DbUpdateException") || typeName.Contains("NpgsqlException"))
            {
                current = current.InnerException;
                continue;
            }

            break;
        }
        return current;
    }

    private static string CleanMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var firstLine = raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        // Remove code prefix like "42703: " or "23505: "
        if (firstLine.Length > 7 && char.IsDigit(firstLine[0]) && firstLine[5] == ':')
        {
            firstLine = firstLine[7..].Trim();
        }
        return firstLine;
    }
}
