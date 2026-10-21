using System.Collections.Concurrent;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AccountingInventory.Application.OCR;
using AccountingInventory.Application.OCR.Models;
using AccountingInventory.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AccountingInventory.Infrastructure.OCR.Engines;

public sealed class GoogleDocumentAiOcrEngine(
    IHttpClientFactory httpClientFactory,
    ILogger<GoogleDocumentAiOcrEngine> logger) : IOcrProviderEngine
{
    public string ProviderType => "GoogleDocumentAi";

    private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresAtUtc)> TokenCache = new();

    public async Task<OcrConnectionTestResult> TestConnectionAsync(
        OcrProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var (getUrl, _, projectId, location, processorId) = ResolveProcessorUrls(config);
            var client = httpClientFactory.CreateClient();

            var accessToken = await GetGoogleAccessTokenAsync(client, config, cancellationToken);

            using var req = new HttpRequestMessage(HttpMethod.Get, getUrl);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            else if (!string.IsNullOrWhiteSpace(config.ApiKeyOrSecret))
            {
                req.Headers.Add("X-Goog-Api-Key", config.ApiKeyOrSecret);
            }
            else
            {
                return new OcrConnectionTestResult(
                    Success: false,
                    Message: "Google Document AI requires a Service Account JSON (in Additional Config) or an API Key / Bearer Token.",
                    ResponseTimeMs: 0
                );
            }

            var resp = await client.SendAsync(req, cancellationToken);
            sw.Stop();
            var respBody = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (resp.IsSuccessStatusCode)
            {
                string displayName = processorId;
                string type = "PROCESSOR";
                string state = "ENABLED";

                try
                {
                    using var procDoc = JsonDocument.Parse(respBody);
                    if (procDoc.RootElement.TryGetProperty("displayName", out var dn)) displayName = dn.GetString() ?? displayName;
                    if (procDoc.RootElement.TryGetProperty("type", out var tp)) type = tp.GetString() ?? type;
                    if (procDoc.RootElement.TryGetProperty("state", out var st)) state = st.GetString() ?? state;
                }
                catch { }

                return new OcrConnectionTestResult(
                    Success: true,
                    Message: $"Connected to Google Document AI processor '{displayName}' (Type: {type}, State: {state}) in project '{projectId}' [{location}].",
                    ResponseTimeMs: sw.ElapsedMilliseconds
                );
            }

            string detail = respBody;
            try
            {
                using var errDoc = JsonDocument.Parse(respBody);
                if (errDoc.RootElement.TryGetProperty("error", out var errObj) &&
                    errObj.TryGetProperty("message", out var msgElem))
                {
                    detail = msgElem.GetString() ?? respBody;
                }
            }
            catch { }

            return new OcrConnectionTestResult(
                Success: false,
                Message: $"Google Document AI returned HTTP {resp.StatusCode}: {detail}",
                ResponseTimeMs: sw.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Failed to test connection to Google Document AI.");
            return new OcrConnectionTestResult(
                Success: false,
                Message: $"Google Document AI connection failed: {ex.Message}",
                ResponseTimeMs: sw.ElapsedMilliseconds
            );
        }
    }

    public async Task<StructuredInvoiceOcrResult> ExtractInvoiceAsync(
        Stream documentStream,
        string fileName,
        string mimeType,
        string? clientRawText,
        OcrProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation("Processing invoice {FileName} via Google Document AI...", fileName);

        var (_, processUrl, _, _, _) = ResolveProcessorUrls(config);
        var client = httpClientFactory.CreateClient();
        var accessToken = await GetGoogleAccessTokenAsync(client, config, cancellationToken);

        byte[] fileBytes;
        if (documentStream is MemoryStream ms)
        {
            fileBytes = ms.ToArray();
        }
        else
        {
            using var copyMs = new MemoryStream();
            if (documentStream.CanSeek) documentStream.Position = 0;
            await documentStream.CopyToAsync(copyMs, cancellationToken);
            fileBytes = copyMs.ToArray();
        }

        var base64Doc = Convert.ToBase64String(fileBytes);
        var requestPayload = new
        {
            rawDocument = new
            {
                content = base64Doc,
                mimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/pdf" : mimeType
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, processUrl);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        else if (!string.IsNullOrWhiteSpace(config.ApiKeyOrSecret))
        {
            request.Headers.Add("X-Goog-Api-Key", config.ApiKeyOrSecret);
        }

        request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorDetail = responseBody;
            try
            {
                using var errDoc = JsonDocument.Parse(responseBody);
                if (errDoc.RootElement.TryGetProperty("error", out var errObj) &&
                    errObj.TryGetProperty("message", out var msgElem))
                {
                    errorDetail = msgElem.GetString() ?? responseBody;
                }
            }
            catch { }

            throw new InvalidOperationException($"Google Document AI API error ({response.StatusCode}): {errorDetail}");
        }

        sw.Stop();
        using var doc = JsonDocument.Parse(responseBody);
        return MapGoogleInvoiceResult(doc, config.Name, sw.ElapsedMilliseconds, clientRawText);
    }

    private static (string GetProcessorUrl, string ProcessUrl, string ProjectId, string Location, string ProcessorId)
        ResolveProcessorUrls(OcrProviderConfig config)
    {
        string projectId = "";
        string location = !string.IsNullOrWhiteSpace(config.Region) ? config.Region.Trim() : "us";
        string processorId = "";

        // 1. Try extracting project_id from AdditionalConfigJson
        if (!string.IsNullOrWhiteSpace(config.AdditionalConfigJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(config.AdditionalConfigJson);
                if (doc.RootElement.TryGetProperty("project_id", out var pid))
                    projectId = pid.GetString() ?? "";
                else if (doc.RootElement.TryGetProperty("projectId", out var pid2))
                    projectId = pid2.GetString() ?? "";

                if (doc.RootElement.TryGetProperty("location", out var loc))
                    location = loc.GetString() ?? location;
            }
            catch { }
        }

        // 2. Parse candidate strings for full resource path: projects/{project}/locations/{location}/processors/{processor}
        var candidate = (config.ModelIdOrProcessorId ?? "") + " " + (config.EndpointUrl ?? "");
        var match = Regex.Match(candidate, @"projects/([^/\s]+)/locations/([^/\s]+)/processors/([^/\s:]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            projectId = match.Groups[1].Value;
            location = match.Groups[2].Value;
            processorId = match.Groups[3].Value;
        }
        else
        {
            processorId = (config.ModelIdOrProcessorId ?? "").Trim();
        }

        if (string.IsNullOrWhiteSpace(processorId))
        {
            throw new InvalidOperationException("Google Document AI Model / Processor ID is required.");
        }

        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("Google Cloud Project ID is required. Please paste your Google Service Account JSON in 'Additional Configuration (JSON)' or specify {\"projectId\": \"your-project-id\"}.");
        }

        // 3. Determine host
        string host;
        if (!string.IsNullOrWhiteSpace(config.EndpointUrl) && config.EndpointUrl.Contains("googleapis.com"))
        {
            var uri = new Uri(config.EndpointUrl);
            host = $"{uri.Scheme}://{uri.Authority}";
        }
        else
        {
            host = location.Equals("eu", StringComparison.OrdinalIgnoreCase)
                ? "https://eu-documentai.googleapis.com"
                : location.Equals("us", StringComparison.OrdinalIgnoreCase)
                ? "https://us-documentai.googleapis.com"
                : $"https://{location}-documentai.googleapis.com";
        }

        var resourcePath = $"v1/projects/{projectId}/locations/{location}/processors/{processorId}";
        var getUrl = $"{host.TrimEnd('/')}/{resourcePath}";
        var processUrl = $"{getUrl}:process";

        return (getUrl, processUrl, projectId, location, processorId);
    }

    private static async Task<string?> GetGoogleAccessTokenAsync(
        HttpClient client, OcrProviderConfig config, CancellationToken cancellationToken)
    {
        // 1. Direct Bearer token
        if (!string.IsNullOrWhiteSpace(config.ApiKeyOrSecret) &&
            (config.ApiKeyOrSecret.StartsWith("ya29.") || config.ApiKeyOrSecret.Contains("Bearer", StringComparison.OrdinalIgnoreCase)))
        {
            return config.ApiKeyOrSecret.Replace("Bearer ", "").Trim();
        }

        // 2. Extract service account JSON
        JsonDocument? saDoc = null;
        if (!string.IsNullOrWhiteSpace(config.AdditionalConfigJson) && config.AdditionalConfigJson.Contains("private_key"))
        {
            try { saDoc = JsonDocument.Parse(config.AdditionalConfigJson); } catch { }
        }
        else if (!string.IsNullOrWhiteSpace(config.ApiKeyOrSecret) && config.ApiKeyOrSecret.Contains("private_key"))
        {
            try { saDoc = JsonDocument.Parse(config.ApiKeyOrSecret); } catch { }
        }

        if (saDoc != null &&
            saDoc.RootElement.TryGetProperty("client_email", out var emailElem) &&
            saDoc.RootElement.TryGetProperty("private_key", out var keyElem))
        {
            var clientEmail = emailElem.GetString();
            var privateKeyPem = keyElem.GetString();

            if (!string.IsNullOrWhiteSpace(clientEmail) && !string.IsNullOrWhiteSpace(privateKeyPem))
            {
                return await CreateServiceAccountAccessTokenAsync(client, clientEmail, privateKeyPem, cancellationToken);
            }
        }

        return null;
    }

    private static async Task<string> CreateServiceAccountAccessTokenAsync(
        HttpClient client, string clientEmail, string privateKeyPem, CancellationToken cancellationToken)
    {
        if (TokenCache.TryGetValue(clientEmail, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(5))
        {
            return cached.Token;
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var now = DateTimeOffset.UtcNow;
        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

        var header = new JwtHeader(signingCredentials);
        var payload = new JwtPayload
        {
            { "iss", clientEmail },
            { "sub", clientEmail },
            { "aud", "https://oauth2.googleapis.com/token" },
            { "iat", now.ToUnixTimeSeconds() },
            { "exp", now.AddHours(1).ToUnixTimeSeconds() },
            { "scope", "https://www.googleapis.com/auth/cloud-platform" }
        };

        var token = new JwtSecurityToken(header, payload);
        var handler = new JwtSecurityTokenHandler();
        var signedJwt = handler.WriteToken(token);

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = signedJwt
        });

        var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google OAuth2 token exchange failed ({tokenResponse.StatusCode}): {tokenJson}");
        }

        using var respDoc = JsonDocument.Parse(tokenJson);
        var accessToken = respDoc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google OAuth2 response did not contain access_token.");

        var expiresIn = respDoc.RootElement.TryGetProperty("expires_in", out var expElem) ? expElem.GetInt32() : 3600;
        TokenCache[clientEmail] = (accessToken, DateTime.UtcNow.AddSeconds(expiresIn));

        return accessToken;
    }

    private static StructuredInvoiceOcrResult MapGoogleInvoiceResult(
        JsonDocument doc,
        string providerName,
        long elapsedMs,
        string? clientRawText)
    {
        string? vendorName = null;
        string? vendorGstin = null;
        string? vendorAddress = null;
        string? billNumber = null;
        string? billDate = null;
        decimal? grandTotal = null;
        string? documentText = null;
        var rawLines = new List<(string Description, decimal Quantity, decimal UnitPrice, decimal TotalAmount)>();

        if (doc.RootElement.TryGetProperty("document", out var documentElem))
        {
            if (documentElem.TryGetProperty("text", out var textElem))
            {
                documentText = textElem.GetString();
            }

            if (documentElem.TryGetProperty("entities", out var entities))
            {
                foreach (var entity in entities.EnumerateArray())
                {
                    if (entity.TryGetProperty("type", out var typeProp))
                    {
                        var entityType = typeProp.GetString();
                        var mentionText = entity.TryGetProperty("mentionText", out var mt) ? mt.GetString() : null;

                        var normalizedType = (entityType ?? "").Trim().ToLowerInvariant();

                        switch (normalizedType)
                        {
                            case "supplier_name":
                            case "vendor_name":
                            case "seller_name":
                            case "vendor":
                            case "supplier":
                                if (!string.IsNullOrWhiteSpace(mentionText)) vendorName = mentionText;
                                break;
                            case "supplier_tax_id":
                            case "supplier_vat_id":
                            case "supplier_gstin":
                            case "gstin":
                            case "tax_id":
                            case "vat_id":
                                if (!string.IsNullOrWhiteSpace(mentionText)) vendorGstin = mentionText;
                                break;
                            case "supplier_address":
                            case "vendor_address":
                            case "seller_address":
                            case "address":
                                if (!string.IsNullOrWhiteSpace(mentionText)) vendorAddress = mentionText;
                                break;
                            case "invoice_id":
                            case "invoice_number":
                            case "bill_number":
                            case "bill_no":
                            case "invoice":
                                if (!string.IsNullOrWhiteSpace(mentionText)) billNumber = mentionText;
                                break;
                            case "invoice_date":
                            case "bill_date":
                            case "date":
                                if (!string.IsNullOrWhiteSpace(mentionText)) billDate = mentionText;
                                break;
                            case "total_amount":
                            case "grand_total":
                            case "invoice_total":
                            case "total":
                                if (decimal.TryParse(mentionText?.Replace(",", ""), out var tot)) grandTotal = tot;
                                break;
                            case "line_item":
                            case "item":
                            case "product":
                            case "line":
                                string desc = "";
                                decimal qty = 1;
                                decimal unitPrice = 0;
                                decimal lineAmount = 0;

                                if (entity.TryGetProperty("properties", out var props))
                                {
                                    foreach (var p in props.EnumerateArray())
                                    {
                                        var pType = (p.TryGetProperty("type", out var pt) ? pt.GetString() : null)?.ToLowerInvariant() ?? "";
                                        var pVal = (p.TryGetProperty("mentionText", out var pv) ? pv.GetString() : null) ?? "";

                                        if ((pType == "line_item/description" || pType == "description" || pType == "product_name" || pType == "item_name" || pType == "item_description" || pType == "name") && !string.IsNullOrWhiteSpace(pVal)) desc = pVal;
                                        if ((pType == "line_item/quantity" || pType == "quantity" || pType == "qty") && decimal.TryParse(pVal.Replace(",", ""), out var q)) qty = q;
                                        if ((pType == "line_item/unit_price" || pType == "unit_price" || pType == "price" || pType == "rate") && decimal.TryParse(pVal.Replace(",", ""), out var up)) unitPrice = up;
                                        if ((pType == "line_item/amount" || pType == "line_item/total_amount" || pType == "amount" || pType == "line_total" || pType == "total") && decimal.TryParse(pVal.Replace(",", ""), out var amt)) lineAmount = amt;
                                    }
                                }

                                if (string.IsNullOrWhiteSpace(desc) && !string.IsNullOrWhiteSpace(mentionText))
                                {
                                    desc = mentionText;
                                }

                                if (!string.IsNullOrWhiteSpace(desc))
                                {
                                    rawLines.Add((desc, qty, unitPrice, lineAmount));
                                }
                                break;
                        }
                    }
                }
            }
        }

        return Common.InvoiceExtractionParser.ParseStructuredInvoice(
            rawDocumentText: documentText,
            clientRawText: clientRawText,
            extractedVendorName: vendorName,
            extractedVendorGstin: vendorGstin,
            extractedVendorAddress: vendorAddress,
            extractedBillNumber: billNumber,
            extractedBillDate: billDate,
            extractedGrandTotal: grandTotal,
            rawLineItems: rawLines,
            providerName: providerName,
            elapsedMs: elapsedMs
        );
    }
}
