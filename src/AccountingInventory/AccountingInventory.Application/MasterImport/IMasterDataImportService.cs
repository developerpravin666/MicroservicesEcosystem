namespace AccountingInventory.Application.MasterImport;

public interface IMasterDataImportService
{
    byte[] GenerateMultiSheetExcelTemplateXlsx();

    Task<byte[]> ExportExistingMastersXlsxAsync(CancellationToken ct = default);

    Task<string> ExportExistingMastersXmlAsync(CancellationToken ct = default);

    Task<MasterImportBatchSummaryDto> StageImportAsync(
        string fileName,
        string csvOrJsonContent,
        Guid? userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<MasterImportBatchSummaryDto>> GetBatchesAsync(CancellationToken ct = default);

    Task<MasterImportBatchSummaryDto?> GetBatchDetailsAsync(Guid batchId, CancellationToken ct = default);

    Task<bool> ToggleRowApprovalAsync(
        Guid batchId,
        Guid rowId,
        bool isApproved,
        CancellationToken ct = default);

    Task<MasterImportBatchSummaryDto> RejectBatchAsync(
        Guid batchId,
        string? reason,
        string reviewer,
        CancellationToken ct = default);

    Task<ApproveImportResult> ApproveAndImportBatchAsync(
        Guid batchId,
        string reviewer,
        string? notes = null,
        CancellationToken ct = default);

    Task<bool> DeleteBatchAsync(
        Guid batchId,
        CancellationToken ct = default);
}

