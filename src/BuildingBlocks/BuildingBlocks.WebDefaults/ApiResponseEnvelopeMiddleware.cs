using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace BuildingBlocks.WebDefaults;

/// <summary>Wraps API success and error results in the shared success/message/data format.</summary>
public sealed class ApiResponseEnvelopeMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var response = context.Response;
        var originalBody = response.Body;
        await using var buffer = new MemoryStream();
        response.Body = buffer;
        try
        {
            await next(context);
        }
        catch
        {
            response.Body = originalBody;
            throw;
        }

        var statusCode = response.StatusCode;
        var headers = response.Headers.ToDictionary(pair => pair.Key, pair => pair.Value);
        var contentType = response.ContentType;
        var bytes = buffer.ToArray();
        response.Body = originalBody;

        // File results must keep their original bytes; decoding a workbook as UTF-8
        // and wrapping it in JSON corrupts the downloaded OpenXML ZIP package.
        if (headers.Keys.Any(key => key.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
            || (statusCode is >= 200 and < 300
                && !string.IsNullOrWhiteSpace(contentType)
                && !contentType.Contains("json", StringComparison.OrdinalIgnoreCase)))
        {
            await ReplayAsync(response, originalBody, statusCode, headers, contentType, bytes);
            return;
        }

        var data = ParseBody(bytes, contentType);
        if (IsEnvelope(data))
        {
            await ReplayAsync(response, originalBody, statusCode, headers, contentType, bytes);
            return;
        }

        var success = statusCode is >= 200 and < 300;
        var message = success
            ? "Request completed successfully."
            : GetFailureMessage(data, statusCode);
        var errors = !success ? GetErrors(data) : null;
        var envelope = new ApiResponse<object?>(success, message, success ? data : null, errors);
        var output = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);

        response.Clear();
        foreach (var header in headers)
        {
            if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                continue;
            response.Headers[header.Key] = header.Value;
        }
        response.StatusCode = statusCode == StatusCodes.Status204NoContent
            ? StatusCodes.Status200OK
            : statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength = output.Length;
        await response.Body.WriteAsync(output);
    }

    private static object? ParseBody(byte[] bytes, string? contentType)
    {
        if (bytes.Length == 0) return null;
        if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try { return JsonDocument.Parse(bytes).RootElement.Clone(); }
            catch (JsonException) { /* Keep malformed JSON readable as text. */ }
        }
        return Encoding.UTF8.GetString(bytes);
    }

    private static bool IsEnvelope(object? value)
        => value is JsonElement { ValueKind: JsonValueKind.Object } json
           && json.TryGetProperty("success", out _)
           && json.TryGetProperty("message", out _)
           && json.TryGetProperty("data", out _);

    private static string GetFailureMessage(object? value, int statusCode)
    {
        if (value is JsonElement json && json.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "detail", "message", "title" })
                if (json.TryGetProperty(key, out var property)
                    && property.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(property.GetString()))
                    return property.GetString()!;
        }
        if (value is string text && !string.IsNullOrWhiteSpace(text)) return text;
        return $"Request failed ({statusCode}).";
    }

    private static IReadOnlyDictionary<string, string[]>? GetErrors(object? value)
    {
        if (value is not JsonElement { ValueKind: JsonValueKind.Object } json
            || !json.TryGetProperty("errors", out var errors)
            || errors.ValueKind != JsonValueKind.Object) return null;

        return errors.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.ValueKind == JsonValueKind.Array
                ? property.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>());
    }

    private static async Task ReplayAsync(
        HttpResponse response,
        Stream originalBody,
        int statusCode,
        Dictionary<string, StringValues> headers,
        string? contentType,
        byte[] bytes)
    {
        response.Clear();
        foreach (var header in headers) response.Headers[header.Key] = header.Value;
        response.StatusCode = statusCode;
        response.ContentType = contentType;
        response.ContentLength = bytes.Length;
        await originalBody.WriteAsync(bytes);
    }
}
