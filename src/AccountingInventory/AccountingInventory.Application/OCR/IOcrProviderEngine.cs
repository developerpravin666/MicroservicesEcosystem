using AccountingInventory.Application.OCR.Models;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.OCR;

public interface IOcrProviderEngine
{
    string ProviderType { get; }
    Task<StructuredInvoiceOcrResult> ExtractInvoiceAsync(
        Stream documentStream,
        string fileName,
        string mimeType,
        string? clientRawText,
        OcrProviderConfig config,
        CancellationToken cancellationToken = default);

    Task<OcrConnectionTestResult> TestConnectionAsync(
        OcrProviderConfig config,
        CancellationToken cancellationToken = default);
}

