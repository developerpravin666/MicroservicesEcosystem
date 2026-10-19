using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using AccountingInventory.Application.OCR;
using AccountingInventory.Application.OCR.Models;
using AccountingInventory.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Infrastructure.OCR.Engines;

public sealed class AzureDocumentIntelligenceOcrEngine(
    IHttpClientFactory httpClientFactory,
    ILogger<AzureDocumentIntelligenceOcrEngine> logger) : IOcrProviderEngine
{
    public string ProviderType => "AzureDocumentIntelligence";

    public async Task<OcrConnectionTestResult> TestConnectionAsync(
        OcrProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(config.EndpointUrl) || string.IsNullOrWhiteSpace(config.ApiKeyOrSecret))
        {
            return new OcrConnectionTestResult(
                Success: false,
                Message: "Azure Document Intelligence requires Endpoint URL and Subscription Key to connect.",
                ResponseTimeMs: 0
            );
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            var baseUrl = config.EndpointUrl.TrimEnd('/');
            // Check Document Models list endpoint for health
            var testUrl = $"{baseUrl}/documentintelligence/documentModels?api-version=2024-02-29-preview";
            using var req = new HttpRequestMessage(HttpMethod.Get, testUrl);
            req.Headers.Add("Ocp-Apim-Subscription-Key", config.ApiKeyOrSecret);

            var resp = await client.SendAsync(req, cancellationToken);
            sw.Stop();

            if (resp.IsSuccessStatusCode)
            {
                return new OcrConnectionTestResult(
                    Success: true,
                    Message: "Azure Document Intelligence connected successfully! Prebuilt invoice models ready.",
                    ResponseTimeMs: sw.ElapsedMilliseconds
                );
            }

            var errBody = await resp.Content.ReadAsStringAsync(cancellationToken);
            return new OcrConnectionTestResult(
                Success: false,
                Message: $"Azure returned HTTP {resp.StatusCode}: {errBody}",
                ResponseTimeMs: sw.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Failed to connect to Azure Document Intelligence.");
            return new OcrConnectionTestResult(
                Success: false,
                Message: $"Connection failed: {ex.Message}",
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
        logger.LogInformation("Processing invoice {FileName} via Azure Document Intelligence...", fileName);

        if (string.IsNullOrWhiteSpace(config.EndpointUrl) || string.IsNullOrWhiteSpace(config.ApiKeyOrSecret))
        {
            throw new InvalidOperationException("Azure Document Intelligence endpoint URL and Subscription Key must be configured by Super Admin.");
        }

        var client = httpClientFactory.CreateClient();
        var baseUrl = config.EndpointUrl.TrimEnd('/');
        var modelId = string.IsNullOrWhiteSpace(config.ModelIdOrProcessorId) ? "prebuilt-invoice" : config.ModelIdOrProcessorId;
        var analyzeUrl = $"{baseUrl}/documentintelligence/documentModels/{modelId}:analyze?api-version=2024-02-29-preview";

        using var request = new HttpRequestMessage(HttpMethod.Post, analyzeUrl);
        request.Headers.Add("Ocp-Apim-Subscription-Key", config.ApiKeyOrSecret);
        using var streamContent = new StreamContent(documentStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType);
        request.Content = streamContent;

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Azure Document Intelligence error ({response.StatusCode}): {err}");
        }

        // Azure returns 202 Accepted with Operation-Location
        if (!response.Headers.TryGetValues("Operation-Location", out var opValues))
        {
            throw new InvalidOperationException("Azure Document Intelligence response missing Operation-Location header.");
        }

        var operationUrl = opValues.First();
        JsonDocument? analyzeResultDoc = null;

        // Poll operation status (up to 30 seconds)
        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(1500, cancellationToken);
            using var pollReq = new HttpRequestMessage(HttpMethod.Get, operationUrl);
            pollReq.Headers.Add("Ocp-Apim-Subscription-Key", config.ApiKeyOrSecret);
            var pollResp = await client.SendAsync(pollReq, cancellationToken);
            var pollBody = await pollResp.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(pollBody);

            if (doc.RootElement.TryGetProperty("status", out var statusProp))
            {
                var status = statusProp.GetString();
                if (status == "succeeded")
                {
                    analyzeResultDoc = doc;
                    break;
                }
                if (status == "failed")
                {
                    throw new InvalidOperationException($"Azure document analysis failed: {pollBody}");
                }
            }
        }

        sw.Stop();

        if (analyzeResultDoc == null)
        {
            throw new TimeoutException("Azure Document Intelligence analysis timed out waiting for completion.");
        }

        return MapAzureInvoiceResult(analyzeResultDoc, config.Name, sw.ElapsedMilliseconds, clientRawText);
    }

    private static StructuredInvoiceOcrResult MapAzureInvoiceResult(
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
        string? contentText = null;
        var rawLines = new List<(string Description, decimal Quantity, decimal UnitPrice, decimal TotalAmount)>();

        if (doc.RootElement.TryGetProperty("analyzeResult", out var analyzeResult))
        {
            if (analyzeResult.TryGetProperty("content", out var contentElem))
            {
                contentText = contentElem.GetString();
            }

            if (analyzeResult.TryGetProperty("documents", out var docs) && docs.GetArrayLength() > 0)
            {
                var firstDoc = docs[0];
                if (firstDoc.TryGetProperty("fields", out var fields))
                {
                    if (fields.TryGetProperty("VendorName", out var vn) && vn.TryGetProperty("content", out var vnc))
                        vendorName = vnc.GetString();

                    if (fields.TryGetProperty("VendorTaxId", out var vt) && vt.TryGetProperty("content", out var vtc))
                        vendorGstin = vtc.GetString();

                    if (fields.TryGetProperty("VendorAddress", out var va) && va.TryGetProperty("content", out var vac))
                        vendorAddress = vac.GetString();

                    if (fields.TryGetProperty("InvoiceId", out var ii) && ii.TryGetProperty("content", out var iic))
                        billNumber = iic.GetString();

                    if (fields.TryGetProperty("InvoiceDate", out var id) && id.TryGetProperty("content", out var idc))
                        billDate = idc.GetString();

                    if (fields.TryGetProperty("InvoiceTotal", out var it) && it.TryGetProperty("valueNumber", out var itn))
                        grandTotal = (decimal)itn.GetDouble();

                    // Line items
                    if (fields.TryGetProperty("Items", out var itemsElem) && itemsElem.TryGetProperty("valueArray", out var arr))
                    {
                        foreach (var itm in arr.EnumerateArray())
                        {
                            if (itm.TryGetProperty("valueObject", out var vo))
                            {
                                string desc = "";
                                decimal qty = 1;
                                decimal unitPrice = 0;
                                decimal totalAmt = 0;

                                if (vo.TryGetProperty("Description", out var d) && d.TryGetProperty("content", out var dc))
                                    desc = dc.GetString() ?? "";

                                if (vo.TryGetProperty("Quantity", out var q) && q.TryGetProperty("valueNumber", out var qn))
                                    qty = (decimal)qn.GetDouble();

                                if (vo.TryGetProperty("UnitPrice", out var up) && up.TryGetProperty("valueNumber", out var upn))
                                    unitPrice = (decimal)upn.GetDouble();

                                if (vo.TryGetProperty("Amount", out var am) && am.TryGetProperty("valueNumber", out var amn))
                                    totalAmt = (decimal)amn.GetDouble();
                                else
                                    totalAmt = qty * unitPrice;

                                if (!string.IsNullOrWhiteSpace(desc))
                                {
                                    rawLines.Add((desc, qty, unitPrice, totalAmt));
                                }
                            }
                        }
                    }
                }
            }
        }

        return Common.InvoiceExtractionParser.ParseStructuredInvoice(
            rawDocumentText: contentText,
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

