using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.MasterImport;

public sealed partial class MasterDataImportService(
    IAccountingInventoryDbContext db,
    ILogger<MasterDataImportService> logger)
    : IMasterDataImportService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };


    public byte[] GenerateMultiSheetExcelTemplateXlsx()
    {
        var sheets = new List<(string SheetName, List<string> Headers, List<List<string>> Rows)>
        {
            (
                "Vendors",
                new List<string> { "Name", "Code", "Mobile", "Email", "Description", "Address" },
                new List<List<string>>()
                {
                    new() { "Apple Distribution India", "VEN-APP-001", "9876543210", "distro@apple.in", "Official Apple national distributor", "BKC Bandra Kurla Complex Mumbai" },
                    new() { "Samsung India Electronics", "VEN-SAM-001", "9876501234", "support@samsung.in", "Official Samsung direct distributor", "Two Horizon Center DLF Phase 5 Gurugram" }
                }
            ),
            (
                "Brands",
                new List<string> { "Name", "Description" },
                new List<List<string>>
                {
                    new() { "Apple", "Premium mobile electronics brand" },
                    new() { "Samsung", "Global consumer electronics brand" },
                    new() { "OnePlus", "Flagship smartphone brand" }
                }
            ),
            (
                "ProductTypes",
                new List<string> { "Name", "Description" },
                new List<List<string>>
                {
                    new() { "Smartphone", "Handheld mobile phones" },
                    new() { "Tablet", "Tablet computers" },
                    new() { "Smartwatch", "Wearable smart devices" }
                }
            ),
            (
                "Models",
                new List<string> { "Name", "Code", "Brand", "ProductType", "Description" },
                new List<List<string>>
                {
                    new() { "iPhone 15 Pro", "IPH15P", "Apple", "Smartphone", "6.1-inch Super Retina XDR display with A17 Pro" },
                    new() { "Galaxy S24 Ultra", "GALS24U", "Samsung", "Smartphone", "6.8-inch Dynamic AMOLED with S-Pen" }
                }
            ),
            (
                "Variants",
                new List<string> { "Name", "Description" },
                new List<List<string>>
                {
                    new() { "128GB", "Base storage tier" },
                    new() { "256GB", "Mid storage tier" },
                    new() { "512GB", "High storage tier" }
                }
            ),
            (
                "Colors",
                new List<string> { "Name", "Description" },
                new List<List<string>>()
                {
                    new() { "Natural Titanium", "Matte titanium finish" },
                    new() { "Phantom Black", "Sleek matte black finish" },
                    new() { "Titanium Blue", "Deep metallic blue" }
                }
            )
        };

        return BuildClosedXmlWorkbook(sheets);
    }
    
    public async Task<byte[]> ExportExistingMastersXlsxAsync(CancellationToken ct = default)
    {
        var brands = await db.Brands.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);
        var types = await db.ProductTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        var brandLookup = brands.ToDictionary(b => b.Id, b => b.Name ?? "");
        var typeLookup = types.ToDictionary(t => t.Id, t => t.Name ?? "");
        var models = await db.ProductModels.AsNoTracking().OrderBy(m => m.Name).ToListAsync(ct);
        var variants = await db.Variants.AsNoTracking().OrderBy(v => v.Name).ToListAsync(ct);
        var colors = await db.Colors.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var vendors = await db.Vendors.AsNoTracking().OrderBy(v => v.Name).ToListAsync(ct);

        var sheets = new List<(string SheetName, List<string> Headers, List<List<string>> Rows)>
        {
            (
                "Vendors",
                new List<string> { "Name", "Code", "Mobile", "Email", "Description", "Address" },
                vendors.Select(v => new List<string>
                {
                    v.Name ?? "",
                    v.Code ?? "",
                    v.Mobile ?? "",
                    v.Email ?? "",
                    v.Description ?? "",
                    v.Address ?? ""
                }).ToList()
            ),
            (
                "Brands",
                new List<string> { "Name", "Description" },
                brands.Select(b => new List<string>
                {
                    b.Name ?? "",
                    b.Description ?? ""
                }).ToList()
            ),
            (
                "ProductTypes",
                new List<string> { "Name", "Description" },
                types.Select(t => new List<string>
                {
                    t.Name ?? "",
                    t.Description ?? ""
                }).ToList()
            ),
            (
                "Models",
                new List<string> { "Name", "Code", "Brand", "ProductType", "Description" },
                models.Select(m => new List<string>
                {
                    m.Name ?? "",
                    m.Code ?? "",
                    brandLookup.GetValueOrDefault(m.BrandId, ""),
                    typeLookup.GetValueOrDefault(m.ProductTypeId, ""),
                    m.Description ?? ""
                }).ToList()
            ),
            (
                "Variants",
                new List<string> { "Name", "Description" },
                variants.Select(v => new List<string>
                {
                    v.Name ?? "",
                    v.Description ?? ""
                }).ToList()
            ),
            (
                "Colors",
                new List<string> { "Name", "Description" },
                colors.Select(c => new List<string>
                {
                    c.Name ?? "",
                    c.Description ?? ""
                }).ToList()
            )
        };

        return BuildClosedXmlWorkbook(sheets);
    }

    public async Task<string> ExportExistingMastersXmlAsync(CancellationToken ct = default)
    {
        var brands = await db.Brands.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);
        var types = await db.ProductTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        var brandLookup = brands.ToDictionary(b => b.Id, b => b.Name);
        var typeLookup = types.ToDictionary(t => t.Id, t => t.Name);
        var models = await db.ProductModels.AsNoTracking().OrderBy(m => m.Name).ToListAsync(ct);
        var variants = await db.Variants.AsNoTracking().OrderBy(v => v.Name).ToListAsync(ct);
        var colors = await db.Colors.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var vendors = await db.Vendors.AsNoTracking().OrderBy(v => v.Name).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");
        sb.AppendLine(" <Styles>");
        sb.AppendLine("  <Style ss:ID=\"Default\" ss:Name=\"Normal\"><Font ss:FontName=\"Calibri\" ss:Size=\"11\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"Header\"><Font ss:FontName=\"Calibri\" ss:Size=\"11\" ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/><Interior ss:Color=\"#1E40AF\" ss:Pattern=\"Solid\"/></Style>");
        sb.AppendLine(" </Styles>");

        // 1. Vendors Sheet
        sb.AppendLine(" <Worksheet ss:Name=\"Vendors\"><Table>");
        sb.AppendLine("  <Row ss:StyleID=\"Header\">");
        sb.AppendLine("   <Cell><Data ss:Type=\"String\">Name</Data></Cell><Cell><Data ss:Type=\"String\">Code</Data></Cell><Cell><Data ss:Type=\"String\">Mobile</Data></Cell><Cell><Data ss:Type=\"String\">Email</Data></Cell><Cell><Data ss:Type=\"String\">Description</Data></Cell><Cell><Data ss:Type=\"String\">Address</Data></Cell>");
        sb.AppendLine("  </Row>");
        foreach (var v in vendors)
        {
            sb.AppendLine($"  <Row><Cell><Data ss:Type=\"String\">{EscapeXml(v.Name)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(v.Code)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(v.Mobile)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(v.Email)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(v.Description)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(v.Address)}</Data></Cell></Row>");
        }
        sb.AppendLine(" </Table></Worksheet>");

        // 2. Brands Sheet
        sb.AppendLine(" <Worksheet ss:Name=\"Brands\"><Table>");
        sb.AppendLine("  <Row ss:StyleID=\"Header\">");
        sb.AppendLine("   <Cell><Data ss:Type=\"String\">Name</Data></Cell><Cell><Data ss:Type=\"String\">Description</Data></Cell>");
        sb.AppendLine("  </Row>");
        foreach (var b in brands)
        {
            sb.AppendLine($"  <Row><Cell><Data ss:Type=\"String\">{EscapeXml(b.Name)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(b.Description)}</Data></Cell></Row>");
        }
        sb.AppendLine(" </Table></Worksheet>");

        // 3. ProductTypes Sheet
        sb.AppendLine(" <Worksheet ss:Name=\"ProductTypes\"><Table>");
        sb.AppendLine("  <Row ss:StyleID=\"Header\">");
        sb.AppendLine("   <Cell><Data ss:Type=\"String\">Name</Data></Cell><Cell><Data ss:Type=\"String\">Description</Data></Cell>");
        sb.AppendLine("  </Row>");
        foreach (var t in types)
        {
            sb.AppendLine($"  <Row><Cell><Data ss:Type=\"String\">{EscapeXml(t.Name)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(t.Description)}</Data></Cell></Row>");
        }
        sb.AppendLine(" </Table></Worksheet>");

        // 4. Models Sheet
        sb.AppendLine(" <Worksheet ss:Name=\"Models\"><Table>");
        sb.AppendLine("  <Row ss:StyleID=\"Header\">");
        sb.AppendLine("   <Cell><Data ss:Type=\"String\">Name</Data></Cell><Cell><Data ss:Type=\"String\">Code</Data></Cell><Cell><Data ss:Type=\"String\">Brand</Data></Cell><Cell><Data ss:Type=\"String\">ProductType</Data></Cell><Cell><Data ss:Type=\"String\">Description</Data></Cell>");
        sb.AppendLine("  </Row>");
        foreach (var m in models)
        {
            brandLookup.TryGetValue(m.BrandId, out var bName);
            typeLookup.TryGetValue(m.ProductTypeId, out var tName);
            sb.AppendLine($"  <Row><Cell><Data ss:Type=\"String\">{EscapeXml(m.Name)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(m.Code)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(bName)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(tName)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(m.Description)}</Data></Cell></Row>");
        }
        sb.AppendLine(" </Table></Worksheet>");

        // 5. Variants Sheet
        sb.AppendLine(" <Worksheet ss:Name=\"Variants\"><Table>");
        sb.AppendLine("  <Row ss:StyleID=\"Header\">");
        sb.AppendLine("   <Cell><Data ss:Type=\"String\">Name</Data></Cell><Cell><Data ss:Type=\"String\">Description</Data></Cell>");
        sb.AppendLine("  </Row>");
        foreach (var v in variants)
        {
            sb.AppendLine($"  <Row><Cell><Data ss:Type=\"String\">{EscapeXml(v.Name)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(v.Description)}</Data></Cell></Row>");
        }
        sb.AppendLine(" </Table></Worksheet>");

        // 6. Colors Sheet
        sb.AppendLine(" <Worksheet ss:Name=\"Colors\"><Table>");
        sb.AppendLine("  <Row ss:StyleID=\"Header\">");
        sb.AppendLine("   <Cell><Data ss:Type=\"String\">Name</Data></Cell><Cell><Data ss:Type=\"String\">Description</Data></Cell>");
        sb.AppendLine("  </Row>");
        foreach (var c in colors)
        {
            sb.AppendLine($"  <Row><Cell><Data ss:Type=\"String\">{EscapeXml(c.Name)}</Data></Cell><Cell><Data ss:Type=\"String\">{EscapeXml(c.Description)}</Data></Cell></Row>");
        }
        sb.AppendLine(" </Table></Worksheet>");

        sb.AppendLine("</Workbook>");
        return sb.ToString();
    }

    public async Task<MasterImportBatchSummaryDto> StageImportAsync(
        string fileName,
        string csvOrJsonContent,
        Guid? userId,
        CancellationToken ct = default)
    {
        await EnsureStagingTablesExistAsync(ct);

        var rawRows = ParseInputContent(csvOrJsonContent);
        if (rawRows.Count == 0)
        {
            throw new BadRequestException("The uploaded file does not contain any valid master data rows.");
        }

        // Fetch existing records for validation and action resolution
        var existingBrands = await db.Brands.AsNoTracking().ToListAsync(ct);
        var existingTypes = await db.ProductTypes.AsNoTracking().ToListAsync(ct);
        var existingModels = await db.ProductModels.AsNoTracking().ToListAsync(ct);
        var existingVariants = await db.Variants.AsNoTracking().ToListAsync(ct);
        var existingColors = await db.Colors.AsNoTracking().ToListAsync(ct);
        var existingVendors = await db.Vendors.AsNoTracking().ToListAsync(ct);

        // Pre-index existing data
        var brandLookup = existingBrands.ToDictionary(b => b.Id, b => b.Name);
        var typeLookup = existingTypes.ToDictionary(t => t.Id, t => t.Name);
        var brandByName = existingBrands.ToDictionary(b => b.Name.Trim().ToLowerInvariant(), b => b);
        var typeByName = existingTypes.ToDictionary(t => t.Name.Trim().ToLowerInvariant(), t => t);
        var modelByCode = existingModels.ToDictionary(m => m.Code.Trim().ToLowerInvariant(), m => m);
        var modelByBrandAndName = existingModels
            .GroupBy(m => $"{brandLookup.GetValueOrDefault(m.BrandId, "").Trim().ToLowerInvariant()}|{m.Name.Trim().ToLowerInvariant()}")
            .ToDictionary(g => g.Key, g => g.First());
        var variantByName = existingVariants.ToDictionary(v => v.Name.Trim().ToLowerInvariant(), v => v);
        var colorByName = existingColors.ToDictionary(c => c.Name.Trim().ToLowerInvariant(), c => c);
        var vendorByCode = existingVendors.ToDictionary(v => v.Code.Trim().ToLowerInvariant(), v => v);
        var vendorByEmail = existingVendors.Where(v => !string.IsNullOrWhiteSpace(v.Email))
            .ToDictionary(v => v.Email.Trim().ToLowerInvariant(), v => v);
        var vendorByMobile = existingVendors.Where(v => !string.IsNullOrWhiteSpace(v.Mobile))
            .ToDictionary(v => v.Mobile.Trim().ToLowerInvariant(), v => v);
        var vendorByName = existingVendors.ToDictionary(v => v.Name.Trim().ToLowerInvariant(), v => v);

        // Also track brands and product types present in THIS batch so Models can reference newly uploaded ones!
        var batchBrandNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batchTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rawRows)
        {
            var normType = NormalizeEntityType(r.EntityType);
            if (normType == "Brand" && !string.IsNullOrWhiteSpace(r.Name)) batchBrandNames.Add(r.Name.Trim());
            if (normType == "ProductType" && !string.IsNullOrWhiteSpace(r.Name)) batchTypeNames.Add(r.Name.Trim());
        }

        var batchNumber = $"IMP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var batchId = Guid.NewGuid();

        var stagingRows = new List<MasterImportStagingRow>();
        int validCount = 0;
        int errorCount = 0;
        int warningCount = 0;
        int createCount = 0;
        int updateCount = 0;

        var entityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < rawRows.Count; i++)
        {
            var row = rawRows[i];
            var normType = NormalizeEntityType(row.EntityType);
            var errors = new List<string>();
            var warnings = new List<string>();
            var action = StagingRowAction.Create;
            var status = StagingRowStatus.Valid;

            if (string.IsNullOrWhiteSpace(normType))
            {
                errors.Add($"Row {i + 1}: Unknown or missing EntityType '{row.EntityType}'. Expected Vendor, Brand, ProductType, Model, Variant, or Color.");
                normType = "Unknown";
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                errors.Add("Name is required.");
            }

            var entityKey = !string.IsNullOrWhiteSpace(row.Code) ? row.Code.Trim() : row.Name.Trim();
            var entityName = row.Name?.Trim() ?? "Unnamed";

            switch (normType)
            {
                case "Brand":
                    if (brandByName.TryGetValue(entityName.ToLowerInvariant(), out var exB))
                    {
                        action = StagingRowAction.Update;
                        var matchReason = $"Matches existing Brand '{exB.Name}'";
                        warnings.Add($"{matchReason} (ID: {exB.Id}). Will update description.");
                        row.MatchedRecord = new MatchedExistingRecordDto
                        {
                            ExistingId = exB.Id.ToString(),
                            MatchReason = matchReason,
                            FieldComparisons = new List<MatchedExistingFieldDto>
                            {
                                new() { FieldName = "Name", ExistingValue = exB.Name, IncomingValue = row.Name, IsMatching = string.Equals(exB.Name?.Trim(), row.Name?.Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Description", ExistingValue = exB.Description, IncomingValue = row.Description, IsMatching = string.Equals((exB.Description ?? "").Trim(), (row.Description ?? "").Trim(), StringComparison.Ordinal) }
                            }
                        };
                    }
                    else
                    {
                        action = StagingRowAction.Create;
                    }
                    break;

                case "ProductType":
                    if (typeByName.TryGetValue(entityName.ToLowerInvariant(), out var exT))
                    {
                        action = StagingRowAction.Update;
                        var matchReason = $"Matches existing Product Type '{exT.Name}'";
                        warnings.Add($"{matchReason} (ID: {exT.Id}). Will update description.");
                        row.MatchedRecord = new MatchedExistingRecordDto
                        {
                            ExistingId = exT.Id.ToString(),
                            MatchReason = matchReason,
                            FieldComparisons = new List<MatchedExistingFieldDto>
                            {
                                new() { FieldName = "Name", ExistingValue = exT.Name, IncomingValue = row.Name, IsMatching = string.Equals(exT.Name?.Trim(), row.Name?.Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Description", ExistingValue = exT.Description, IncomingValue = row.Description, IsMatching = string.Equals((exT.Description ?? "").Trim(), (row.Description ?? "").Trim(), StringComparison.Ordinal) }
                            }
                        };
                    }
                    else
                    {
                        action = StagingRowAction.Create;
                    }
                    break;

                case "Model":
                    if (string.IsNullOrWhiteSpace(row.Code))
                    {
                        errors.Add("Model Code is required.");
                    }

                    if (string.IsNullOrWhiteSpace(row.Brand))
                    {
                        errors.Add("Brand is required for Model.");
                    }
                    else
                    {
                        var brandKey = row.Brand.Trim().ToLowerInvariant();
                        if (!brandByName.ContainsKey(brandKey) && !batchBrandNames.Contains(row.Brand.Trim()))
                        {
                            errors.Add($"Brand '{row.Brand}' was not found in catalog or earlier rows of this import batch.");
                        }
                    }

                    if (string.IsNullOrWhiteSpace(row.ProductType))
                    {
                        errors.Add("ProductType is required for Model.");
                    }
                    else
                    {
                        var typeKey = row.ProductType.Trim().ToLowerInvariant();
                        if (!typeByName.ContainsKey(typeKey) && !batchTypeNames.Contains(row.ProductType.Trim()))
                        {
                            errors.Add($"Product Type '{row.ProductType}' was not found in catalog or earlier rows of this import batch.");
                        }
                    }

                    ProductModel? exM = null;
                    string modelMatchReason = "";
                    if (!string.IsNullOrWhiteSpace(row.Code) && modelByCode.TryGetValue(row.Code.Trim().ToLowerInvariant(), out var mCode))
                    {
                        exM = mCode;
                        modelMatchReason = $"Matches existing Model by Code '{mCode.Code}' ('{mCode.Name}')";
                    }
                    else if (!string.IsNullOrWhiteSpace(row.Brand) && !string.IsNullOrWhiteSpace(row.Name) &&
                        modelByBrandAndName.TryGetValue($"{row.Brand.Trim().ToLowerInvariant()}|{row.Name.Trim().ToLowerInvariant()}", out var mBrandName))
                    {
                        exM = mBrandName;
                        modelMatchReason = $"Matches existing Model by Brand & Name '{row.Brand} - {mBrandName.Name}'";
                    }

                    if (exM != null)
                    {
                        action = StagingRowAction.Update;
                        warnings.Add($"{modelMatchReason}. Will update model details.");
                        brandLookup.TryGetValue(exM.BrandId, out var existingBrandName);
                        typeLookup.TryGetValue(exM.ProductTypeId, out var existingTypeName);

                        row.MatchedRecord = new MatchedExistingRecordDto
                        {
                            ExistingId = exM.Id.ToString(),
                            MatchReason = modelMatchReason,
                            FieldComparisons = new List<MatchedExistingFieldDto>
                            {
                                new() { FieldName = "Name", ExistingValue = exM.Name, IncomingValue = row.Name, IsMatching = string.Equals(exM.Name?.Trim(), row.Name?.Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Code", ExistingValue = exM.Code, IncomingValue = row.Code, IsMatching = string.Equals((exM.Code ?? "").Trim(), (row.Code ?? "").Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Brand", ExistingValue = existingBrandName, IncomingValue = row.Brand, IsMatching = string.Equals((existingBrandName ?? "").Trim(), (row.Brand ?? "").Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "ProductType", ExistingValue = existingTypeName, IncomingValue = row.ProductType, IsMatching = string.Equals((existingTypeName ?? "").Trim(), (row.ProductType ?? "").Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Description", ExistingValue = exM.Description, IncomingValue = row.Description, IsMatching = string.Equals((exM.Description ?? "").Trim(), (row.Description ?? "").Trim(), StringComparison.Ordinal) }
                            }
                        };
                    }
                    else
                    {
                        action = StagingRowAction.Create;
                    }
                    break;

                case "Variant":
                    if (variantByName.TryGetValue(entityName.ToLowerInvariant(), out var exV))
                    {
                        action = StagingRowAction.Update;
                        var matchReason = $"Matches existing Variant '{exV.Name}'";
                        warnings.Add($"{matchReason} (ID: {exV.Id}). Will update description.");
                        row.MatchedRecord = new MatchedExistingRecordDto
                        {
                            ExistingId = exV.Id.ToString(),
                            MatchReason = matchReason,
                            FieldComparisons = new List<MatchedExistingFieldDto>
                            {
                                new() { FieldName = "Name", ExistingValue = exV.Name, IncomingValue = row.Name, IsMatching = string.Equals(exV.Name?.Trim(), row.Name?.Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Description", ExistingValue = exV.Description, IncomingValue = row.Description, IsMatching = string.Equals((exV.Description ?? "").Trim(), (row.Description ?? "").Trim(), StringComparison.Ordinal) }
                            }
                        };
                    }
                    else
                    {
                        action = StagingRowAction.Create;
                    }
                    break;

                case "Color":
                    if (colorByName.TryGetValue(entityName.ToLowerInvariant(), out var exC))
                    {
                        action = StagingRowAction.Update;
                        var matchReason = $"Matches existing Color '{exC.Name}'";
                        warnings.Add($"{matchReason} (ID: {exC.Id}). Will update description.");
                        row.MatchedRecord = new MatchedExistingRecordDto
                        {
                            ExistingId = exC.Id.ToString(),
                            MatchReason = matchReason,
                            FieldComparisons = new List<MatchedExistingFieldDto>
                            {
                                new() { FieldName = "Name", ExistingValue = exC.Name, IncomingValue = row.Name, IsMatching = string.Equals(exC.Name?.Trim(), row.Name?.Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Description", ExistingValue = exC.Description, IncomingValue = row.Description, IsMatching = string.Equals((exC.Description ?? "").Trim(), (row.Description ?? "").Trim(), StringComparison.Ordinal) }
                            }
                        };
                    }
                    else
                    {
                        action = StagingRowAction.Create;
                    }
                    break;

                case "Vendor":
                    if (string.IsNullOrWhiteSpace(row.Code)) errors.Add("Vendor Code is required.");
                    if (string.IsNullOrWhiteSpace(row.Mobile)) errors.Add("Vendor Mobile is required.");
                    else if (!Regex.IsMatch(row.Mobile.Trim(), @"^[0-9+\-\s]{7,20}$"))
                    {
                        errors.Add("Vendor Mobile number must contain 7 to 20 digits.");
                    }

                    if (string.IsNullOrWhiteSpace(row.Email)) errors.Add("Vendor Email is required.");
                    else if (!row.Email.Contains('@') || !row.Email.Contains('.'))
                    {
                        errors.Add("Vendor Email is invalid.");
                    }

                    // Check existing matches
                    Vendor? matchedVendor = null;
                    string vendorMatchReason = "";
                    if (!string.IsNullOrWhiteSpace(row.Code) && vendorByCode.TryGetValue(row.Code.Trim().ToLowerInvariant(), out var vCode))
                    {
                        matchedVendor = vCode;
                        vendorMatchReason = $"Matches existing Vendor by Code '{vCode.Code}' ('{vCode.Name}')";
                    }
                    else if (!string.IsNullOrWhiteSpace(row.Email) && vendorByEmail.TryGetValue(row.Email.Trim().ToLowerInvariant(), out var vEmail))
                    {
                        matchedVendor = vEmail;
                        vendorMatchReason = $"Matches existing Vendor by Email '{vEmail.Email}' ('{vEmail.Name}')";
                    }
                    else if (!string.IsNullOrWhiteSpace(row.Mobile) && vendorByMobile.TryGetValue(row.Mobile.Trim().ToLowerInvariant(), out var vMob))
                    {
                        matchedVendor = vMob;
                        vendorMatchReason = $"Matches existing Vendor by Mobile '{vMob.Mobile}' ('{vMob.Name}')";
                    }
                    else if (vendorByName.TryGetValue(entityName.ToLowerInvariant(), out var vName))
                    {
                        matchedVendor = vName;
                        vendorMatchReason = $"Matches existing Vendor by Name '{vName.Name}' (Code: '{vName.Code}')";
                    }

                    if (matchedVendor != null)
                    {
                        action = StagingRowAction.Update;
                        warnings.Add($"{vendorMatchReason}. Will update contact and address.");
                        row.MatchedRecord = new MatchedExistingRecordDto
                        {
                            ExistingId = matchedVendor.Id.ToString(),
                            MatchReason = vendorMatchReason,
                            FieldComparisons = new List<MatchedExistingFieldDto>
                            {
                                new() { FieldName = "Name", ExistingValue = matchedVendor.Name, IncomingValue = row.Name, IsMatching = string.Equals(matchedVendor.Name?.Trim(), row.Name?.Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Code", ExistingValue = matchedVendor.Code, IncomingValue = row.Code, IsMatching = string.Equals((matchedVendor.Code ?? "").Trim(), (row.Code ?? "").Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Mobile", ExistingValue = matchedVendor.Mobile, IncomingValue = row.Mobile, IsMatching = string.Equals((matchedVendor.Mobile ?? "").Trim(), (row.Mobile ?? "").Trim(), StringComparison.Ordinal) },
                                new() { FieldName = "Email", ExistingValue = matchedVendor.Email, IncomingValue = row.Email, IsMatching = string.Equals((matchedVendor.Email ?? "").Trim(), (row.Email ?? "").Trim(), StringComparison.OrdinalIgnoreCase) },
                                new() { FieldName = "Address", ExistingValue = matchedVendor.Address, IncomingValue = row.Address, IsMatching = string.Equals((matchedVendor.Address ?? "").Trim(), (row.Address ?? "").Trim(), StringComparison.Ordinal) },
                                new() { FieldName = "Description", ExistingValue = matchedVendor.Description, IncomingValue = row.Description, IsMatching = string.Equals((matchedVendor.Description ?? "").Trim(), (row.Description ?? "").Trim(), StringComparison.Ordinal) }
                            }
                        };
                    }
                    else
                    {
                        action = StagingRowAction.Create;
                    }
                    break;
            }

            if (errors.Count > 0)
            {
                status = StagingRowStatus.Invalid;
                errorCount++;
            }
            else if (warnings.Count > 0)
            {
                status = StagingRowStatus.Warning;
                warningCount++;
                validCount++;
            }
            else
            {
                status = StagingRowStatus.Valid;
                validCount++;
            }

            if (action == StagingRowAction.Create) createCount++;
            else if (action == StagingRowAction.Update) updateCount++;

            entityCounts[normType] = entityCounts.GetValueOrDefault(normType, 0) + 1;

            var allMsgs = errors.Concat(warnings).ToList();
            var validationText = allMsgs.Count > 0 ? string.Join(" | ", allMsgs) : null;
            var rawJson = JsonSerializer.Serialize(row, JsonOpts);

            stagingRows.Add(MasterImportStagingRow.Create(
                batchId: batchId,
                rowIndex: i + 1,
                entityType: normType,
                action: action,
                status: status,
                entityKey: entityKey,
                entityName: entityName,
                rawDataJson: rawJson,
                validationErrors: validationText));
        }

        var summaryJson = JsonSerializer.Serialize(entityCounts, JsonOpts);

        var batch = MasterImportBatch.Create(
            batchNumber: batchNumber,
            fileName: fileName,
            fileType: fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? "Excel (.xlsx)" : "Excel/CSV (.csv)",
            totalRows: rawRows.Count,
            validRows: validCount,
            errorRows: errorCount,
            warningRows: warningCount,
            createdCount: createCount,
            updatedCount: updateCount,
            summaryJson: summaryJson,
            createdBy: userId);

        // Inject the generated batch Id so children match
        typeof(MasterImportBatch).GetProperty("Id")?.SetValue(batch, batchId);

        db.MasterImportBatches.Add(batch);
        db.MasterImportStagingRows.AddRange(stagingRows);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Master import batch {BatchNumber} staged with {Total} rows ({Valid} valid, {Errors} errors).",
            batchNumber, rawRows.Count, validCount, errorCount);

        return ToBatchSummaryDto(batch, stagingRows);
    }

    public async Task<IReadOnlyList<MasterImportBatchSummaryDto>> GetBatchesAsync(CancellationToken ct = default)
    {
        await EnsureStagingTablesExistAsync(ct);

        var batches = await db.MasterImportBatches
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return batches.Select(b => ToBatchSummaryDto(b, null)).ToList();
    }

    public async Task<MasterImportBatchSummaryDto?> GetBatchDetailsAsync(Guid batchId, CancellationToken ct = default)
    {
        await EnsureStagingTablesExistAsync(ct);

        var batch = await db.MasterImportBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == batchId, ct);

        if (batch == null) return null;

        var rows = await db.MasterImportStagingRows
            .AsNoTracking()
            .Where(r => r.BatchId == batchId)
            .OrderBy(r => r.RowIndex)
            .ToListAsync(ct);

        return ToBatchSummaryDto(batch, rows);
    }

    public async Task<bool> ToggleRowApprovalAsync(
        Guid batchId,
        Guid rowId,
        bool isApproved,
        CancellationToken ct = default)
    {
        await EnsureStagingTablesExistAsync(ct);

        var row = await db.MasterImportStagingRows
            .SingleOrDefaultAsync(r => r.BatchId == batchId && r.Id == rowId, ct);

        if (row == null) return false;

        row.SetApproved(isApproved);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<MasterImportBatchSummaryDto> RejectBatchAsync(
        Guid batchId,
        string? reason,
        string reviewer,
        CancellationToken ct = default)
    {
        await EnsureStagingTablesExistAsync(ct);

        var batch = await db.MasterImportBatches
            .SingleOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new NotFoundException($"Import batch with ID '{batchId}' not found.");

        if (batch.Status == MasterImportStatus.Approved)
        {
            throw new ConflictException("Cannot reject a batch that has already been approved and imported.");
        }

        batch.Reject(reviewer, reason);
        await db.SaveChangesAsync(ct);

        return ToBatchSummaryDto(batch, null);
    }

    public async Task<bool> DeleteBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        await EnsureStagingTablesExistAsync(ct);

        var batch = await db.MasterImportBatches
            .SingleOrDefaultAsync(b => b.Id == batchId, ct);

        if (batch == null) return false;

        var stagingRows = await db.MasterImportStagingRows
            .Where(r => r.BatchId == batchId)
            .ToListAsync(ct);

        if (stagingRows.Count > 0)
        {
            db.MasterImportStagingRows.RemoveRange(stagingRows);
        }

        db.MasterImportBatches.Remove(batch);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Deleted master import batch {BatchNumber} ({BatchId}) and {RowCount} staging rows.",
            batch.BatchNumber, batchId, stagingRows.Count);

        return true;
    }

    public async Task<ApproveImportResult> ApproveAndImportBatchAsync(
        Guid batchId,
        string reviewer,
        string? notes = null,
        CancellationToken ct = default)
    {
        await EnsureStagingTablesExistAsync(ct);

        var batch = await db.MasterImportBatches
            .SingleOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new NotFoundException($"Import batch with ID '{batchId}' not found.");

        if (batch.Status != MasterImportStatus.PendingVerification && batch.Status != MasterImportStatus.PartiallyApproved)
        {
            throw new ConflictException($"Batch '{batch.BatchNumber}' is in status '{batch.Status}' and cannot be imported.");
        }

        var stagingRows = await db.MasterImportStagingRows
            .Where(r => r.BatchId == batchId && r.IsApproved && !r.IsImported && r.Status != StagingRowStatus.Invalid)
            .OrderBy(r => r.RowIndex)
            .ToListAsync(ct);

        if (stagingRows.Count == 0)
        {
            throw new BadRequestException("No valid approved rows available to import in this batch.");
        }

        int brandCount = 0;
        int typeCount = 0;
        int modelCount = 0;
        int variantCount = 0;
        int colorCount = 0;
        int vendorCount = 0;

        // Step 1: Process Brands
        var brandRows = stagingRows.Where(r => r.EntityType == "Brand").ToList();
        foreach (var r in brandRows)
        {
            var data = DeserializeRowData(r.RawDataJson);
            var existing = await db.Brands.FirstOrDefaultAsync(b => b.Name.ToLower() == data.Name.ToLower().Trim(), ct);
            if (existing != null)
            {
                var updated = Brand.Update(existing.Id, data.Name, data.Description, existing.IsActive);
                db.Brands.Entry(existing).CurrentValues.SetValues(updated);
            }
            else
            {
                var created = Brand.Create(data.Name, data.Description);
                db.Brands.Add(created);
            }
            r.MarkImported("Brand imported successfully.");
            brandCount++;
        }
        await db.SaveChangesAsync(ct);

        // Step 2: Process Product Types
        var typeRows = stagingRows.Where(r => r.EntityType == "ProductType").ToList();
        foreach (var r in typeRows)
        {
            var data = DeserializeRowData(r.RawDataJson);
            var existing = await db.ProductTypes.FirstOrDefaultAsync(t => t.Name.ToLower() == data.Name.ToLower().Trim(), ct);
            if (existing != null)
            {
                var updated = ProductType.Update(existing.Id, data.Name, data.Description, existing.IsActive);
                db.ProductTypes.Entry(existing).CurrentValues.SetValues(updated);
            }
            else
            {
                var created = ProductType.Create(data.Name, data.Description);
                db.ProductTypes.Add(created);
            }
            r.MarkImported("Product Type imported successfully.");
            typeCount++;
        }
        await db.SaveChangesAsync(ct);

        // Fetch refreshed Brands & Types to resolve IDs for Models
        var allBrands = await db.Brands.AsNoTracking().ToDictionaryAsync(b => b.Name.Trim().ToLowerInvariant(), b => b.Id, ct);
        var allTypes = await db.ProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Name.Trim().ToLowerInvariant(), t => t.Id, ct);

        // Step 3: Process Models
        var modelRows = stagingRows.Where(r => r.EntityType == "Model").ToList();
        foreach (var r in modelRows)
        {
            var data = DeserializeRowData(r.RawDataJson);
            var brandKey = data.Brand?.Trim().ToLowerInvariant() ?? "";
            var typeKey = data.ProductType?.Trim().ToLowerInvariant() ?? "";

            if (!allBrands.TryGetValue(brandKey, out var brandId))
            {
                r.MarkFailed($"Cannot import model: Brand '{data.Brand}' was not found.");
                continue;
            }

            if (!allTypes.TryGetValue(typeKey, out var typeId))
            {
                r.MarkFailed($"Cannot import model: Product Type '{data.ProductType}' was not found.");
                continue;
            }

            var modelCode = data.Code?.Trim() ?? data.Name.Trim();
            var existing = await db.ProductModels.FirstOrDefaultAsync(m => m.Code.ToLower() == modelCode.ToLower(), ct);
            if (existing != null)
            {
                var updated = ProductModel.Update(existing.Id, brandId, typeId, modelCode, data.Name, data.Description, existing.IsActive);
                db.ProductModels.Entry(existing).CurrentValues.SetValues(updated);
            }
            else
            {
                var created = ProductModel.Create(brandId, typeId, modelCode, data.Name, data.Description);
                db.ProductModels.Add(created);
            }
            r.MarkImported("Model imported successfully.");
            modelCount++;
        }
        await db.SaveChangesAsync(ct);

        // Step 4: Process Variants
        var variantRows = stagingRows.Where(r => r.EntityType == "Variant").ToList();
        foreach (var r in variantRows)
        {
            var data = DeserializeRowData(r.RawDataJson);
            var existing = await db.Variants.FirstOrDefaultAsync(v => v.Name.ToLower() == data.Name.ToLower().Trim(), ct);
            if (existing != null)
            {
                var updated = Variant.Update(existing.Id, data.Name, data.Description, existing.IsActive);
                db.Variants.Entry(existing).CurrentValues.SetValues(updated);
            }
            else
            {
                var created = Variant.Create(data.Name, data.Description);
                db.Variants.Add(created);
            }
            r.MarkImported("Variant imported successfully.");
            variantCount++;
        }
        await db.SaveChangesAsync(ct);

        // Step 5: Process Colors
        var colorRows = stagingRows.Where(r => r.EntityType == "Color").ToList();
        foreach (var r in colorRows)
        {
            var data = DeserializeRowData(r.RawDataJson);
            var existing = await db.Colors.FirstOrDefaultAsync(c => c.Name.ToLower() == data.Name.ToLower().Trim(), ct);
            if (existing != null)
            {
                var updated = Color.Update(existing.Id, data.Name, data.Description, existing.IsActive);
                db.Colors.Entry(existing).CurrentValues.SetValues(updated);
            }
            else
            {
                var created = Color.Create(data.Name, data.Description);
                db.Colors.Add(created);
            }
            r.MarkImported("Color imported successfully.");
            colorCount++;
        }
        await db.SaveChangesAsync(ct);

        // Step 6: Process Vendors
        var vendorRows = stagingRows.Where(r => r.EntityType == "Vendor").ToList();
        foreach (var r in vendorRows)
        {
            var data = DeserializeRowData(r.RawDataJson);
            var vCode = data.Code?.Trim() ?? data.Name.Trim();
            var existing = await db.Vendors.FirstOrDefaultAsync(v => v.Code.ToLower() == vCode.ToLower(), ct)
                ?? await db.Vendors.FirstOrDefaultAsync(v => v.Email.ToLower() == data.Email!.ToLower().Trim(), ct);

            if (existing != null)
            {
                var updated = Vendor.Update(
                    existing.Id,
                    data.Name,
                    vCode,
                    data.Mobile?.Trim() ?? existing.Mobile,
                    data.Email?.Trim() ?? existing.Email,
                    existing.IsActive,
                    data.Description ?? existing.Description,
                    data.Address ?? existing.Address);
                db.Vendors.Entry(existing).CurrentValues.SetValues(updated);
            }
            else
            {
                var created = Vendor.Create(
                    data.Name,
                    vCode,
                    data.Mobile?.Trim() ?? "9999999999",
                    data.Email?.Trim() ?? "vendor@store.local",
                    data.Description,
                    data.Address);
                db.Vendors.Add(created);
            }
            r.MarkImported("Vendor imported successfully.");
            vendorCount++;
        }
        await db.SaveChangesAsync(ct);

        // Finalize batch
        int totalImported = brandCount + typeCount + modelCount + variantCount + colorCount + vendorCount;
        batch.Approve(reviewer, notes ?? $"Imported {totalImported} records to main catalog.");
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Import batch {BatchNumber} approved and imported: {Total} total records.",
            batch.BatchNumber, totalImported);

        return new ApproveImportResult(
            Success: true,
            BatchId: batch.Id,
            BatchNumber: batch.BatchNumber,
            Status: batch.Status.ToString(),
            TotalImported: totalImported,
            BrandsImported: brandCount,
            ProductTypesImported: typeCount,
            ModelsImported: modelCount,
            VariantsImported: variantCount,
            ColorsImported: colorCount,
            VendorsImported: vendorCount,
            Message: $"Successfully imported {totalImported} records into the main catalog database.",
            CompletedAt: DateTimeOffset.UtcNow);
    }

    private static Task EnsureStagingTablesExistAsync(CancellationToken ct)
    {
        // Staging tables are provisioned by EF Core migration 20261028120000_MasterDataImportStaging
        // and TenantSchemaMigrator across all tenant schemas.
        return Task.CompletedTask;
    }

    private static List<MasterImportRowDto> ParseInputContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return new();

        var trimmed = content.Trim();

        // 1. Check if OpenXML / .xlsx base64
        if (IsLikelyZipOrOpenXml(trimmed, out var zipBytes))
        {
            var closedXmlRows = ParseClosedXmlSpreadsheet(zipBytes);
            if (closedXmlRows.Count > 0) return closedXmlRows;

            var openXmlRows = ParseOpenXmlSpreadsheet(zipBytes);
            if (openXmlRows.Count > 0) return openXmlRows;
        }

        // 2. Check if SpreadsheetML XML (multi-sheet workbook)
        if (trimmed.Contains("<Workbook", StringComparison.OrdinalIgnoreCase) &&
            trimmed.Contains("<Worksheet", StringComparison.OrdinalIgnoreCase))
        {
            var xmlRows = ParseSpreadsheetMlXml(trimmed);
            if (xmlRows.Count > 0) return xmlRows;
        }

        // 3. Check if content is JSON array
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<MasterImportRowDto>>(trimmed, JsonOpts);
                if (list != null && list.Count > 0) return list;
            }
            catch
            {
            }
        }

        return new List<MasterImportRowDto>();
    }

    private static bool IsLikelyZipOrOpenXml(string content, out byte[] zipBytes)
    {
        zipBytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(content)) return false;
        var clean = content.Trim();
        if (clean.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIdx = clean.IndexOf(',');
            if (commaIdx >= 0) clean = clean[(commaIdx + 1)..].Trim();
        }

        try
        {
            var bytes = Convert.FromBase64String(clean);
            if (bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B)
            {
                zipBytes = bytes;
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static List<MasterImportRowDto> ParseSpreadsheetMlXml(string xmlContent)
    {
        var rows = new List<MasterImportRowDto>();
        try
        {
            var doc = XDocument.Parse(xmlContent);
            XNamespace ss = "urn:schemas-microsoft-com:office:spreadsheet";

            var worksheets = doc.Descendants(ss + "Worksheet").ToList();
            if (worksheets.Count == 0)
            {
                worksheets = doc.Descendants().Where(e => e.Name.LocalName.Equals("Worksheet", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            foreach (var ws in worksheets)
            {
                var sheetName = ws.Attribute(ss + "Name")?.Value ?? ws.Attribute("Name")?.Value ?? ws.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
                var defaultEntityType = NormalizeEntityType(sheetName);

                var table = ws.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Table", StringComparison.OrdinalIgnoreCase));
                if (table == null) continue;

                var rowElements = table.Elements().Where(e => e.Name.LocalName.Equals("Row", StringComparison.OrdinalIgnoreCase)).ToList();
                if (rowElements.Count < 2) continue;

                var headers = new List<string>();
                var headerCells = rowElements[0].Elements().Where(e => e.Name.LocalName.Equals("Cell", StringComparison.OrdinalIgnoreCase));
                foreach (var cell in headerCells)
                {
                    var dataVal = cell.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Data", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? "";
                    headers.Add(dataVal.ToLowerInvariant());
                }

                for (int r = 1; r < rowElements.Count; r++)
                {
                    var rowEl = rowElements[r];
                    var cells = rowEl.Elements().Where(e => e.Name.LocalName.Equals("Cell", StringComparison.OrdinalIgnoreCase)).ToList();
                    var cellValues = new List<string>();
                    foreach (var cell in cells)
                    {
                        var dataVal = cell.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Data", StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? "";
                        cellValues.Add(dataVal);
                    }

                    if (cellValues.All(string.IsNullOrWhiteSpace)) continue;

                    var dto = new MasterImportRowDto { EntityType = defaultEntityType };
                    MapRowFields(dto, headers, cellValues);
                    if (string.IsNullOrWhiteSpace(dto.EntityType)) dto.EntityType = defaultEntityType;

                    if (!string.IsNullOrWhiteSpace(dto.Name) || !string.IsNullOrWhiteSpace(dto.EntityType))
                    {
                        rows.Add(dto);
                    }
                }
            }
        }
        catch
        {
            // fallback
        }
        return rows;
    }


    private static List<MasterImportRowDto> ParseOpenXmlSpreadsheet(byte[] bytes)
    {
        var rows = new List<MasterImportRowDto>();
        try
        {
            using var ms = new MemoryStream(bytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

            var sharedStrings = new List<string>();
            var sstEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sstEntry != null)
            {
                using var sstStream = sstEntry.Open();
                var sstDoc = XDocument.Load(sstStream);
                foreach (var si in sstDoc.Descendants().Where(e => e.Name.LocalName == "si"))
                {
                    var text = string.Concat(si.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value));
                    sharedStrings.Add(text);
                }
            }

            var wbEntry = archive.GetEntry("xl/workbook.xml");
            var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (wbEntry == null) return rows;

            var rIdToTarget = new Dictionary<string, string>();
            if (relsEntry != null)
            {
                using var relsStream = relsEntry.Open();
                var relsDoc = XDocument.Load(relsStream);
                foreach (var rel in relsDoc.Descendants().Where(e => e.Name.LocalName == "Relationship"))
                {
                    var id = rel.Attribute("Id")?.Value;
                    var target = rel.Attribute("Target")?.Value;
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                    {
                        var normTarget = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
                        rIdToTarget[id] = normTarget;
                    }
                }
            }

            using var wbStream = wbEntry.Open();
            var wbDoc = XDocument.Load(wbStream);
            var sheetNodes = wbDoc.Descendants().Where(e => e.Name.LocalName == "sheet").ToList();

            foreach (var sheetNode in sheetNodes)
            {
                var sheetName = sheetNode.Attribute("name")?.Value ?? "";
                var rId = sheetNode.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value ?? "";
                var defaultEntityType = NormalizeEntityType(sheetName);

                if (!rIdToTarget.TryGetValue(rId, out var sheetPath))
                {
                    sheetPath = $"xl/worksheets/sheet{sheetNodes.IndexOf(sheetNode) + 1}.xml";
                }

                var sheetEntry = archive.GetEntry(sheetPath);
                if (sheetEntry == null) continue;

                using var sheetStream = sheetEntry.Open();
                var sheetDoc = XDocument.Load(sheetStream);
                var rowNodes = sheetDoc.Descendants().Where(e => e.Name.LocalName == "row").ToList();
                if (rowNodes.Count < 2) continue;

                var headerMap = new Dictionary<int, string>();
                var headerRow = rowNodes[0];
                int maxCol = 0;
                foreach (var cell in headerRow.Elements().Where(e => e.Name.LocalName == "c"))
                {
                    var colIdx = GetColumnIndexFromRef(cell.Attribute("r")?.Value);
                    headerMap[colIdx] = GetOpenXmlCellValue(cell, sharedStrings).ToLowerInvariant();
                    if (colIdx >= maxCol) maxCol = colIdx + 1;
                }

                var headers = new List<string>();
                for (int c = 0; c < maxCol; c++)
                {
                    headers.Add(headerMap.GetValueOrDefault(c, ""));
                }

                for (int r = 1; r < rowNodes.Count; r++)
                {
                    var rowEl = rowNodes[r];
                    var cells = rowEl.Elements().Where(e => e.Name.LocalName == "c").ToList();
                    var cellMap = new Dictionary<int, string>();
                    foreach (var cell in cells)
                    {
                        var colIdx = GetColumnIndexFromRef(cell.Attribute("r")?.Value);
                        cellMap[colIdx] = GetOpenXmlCellValue(cell, sharedStrings);
                    }

                    var cellValues = new List<string>();
                    for (int c = 0; c < maxCol; c++)
                    {
                        cellValues.Add(cellMap.GetValueOrDefault(c, ""));
                    }

                    if (cellValues.All(string.IsNullOrWhiteSpace)) continue;

                    var dto = new MasterImportRowDto { EntityType = defaultEntityType };
                    MapRowFields(dto, headers, cellValues);
                    if (string.IsNullOrWhiteSpace(dto.EntityType)) dto.EntityType = defaultEntityType;

                    if (!string.IsNullOrWhiteSpace(dto.Name) || !string.IsNullOrWhiteSpace(dto.EntityType))
                    {
                        rows.Add(dto);
                    }
                }
            }
        }
        catch
        {
            // fallback
        }
        return rows;
    }

    private static int GetColumnIndexFromRef(string? cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return 0;
        int col = 0;
        foreach (char ch in cellRef)
        {
            if (char.IsLetter(ch))
            {
                col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            }
            else break;
        }
        return Math.Max(0, col - 1);
    }

    private static string GetOpenXmlCellValue(XElement cell, List<string> sharedStrings)
    {
        var typeAttr = cell.Attribute("t")?.Value;
        var valElem = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "v");
        var val = valElem?.Value ?? "";

        if (typeAttr == "s" && int.TryParse(val, out var idx) && idx >= 0 && idx < sharedStrings.Count)
        {
            return sharedStrings[idx];
        }

        if (typeAttr == "inlineStr")
        {
            return string.Concat(cell.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value));
        }

        return val;
    }

    private static void MapRowFields(MasterImportRowDto dto, List<string> headers, List<string> cols)
    {
        dto.IsActive = true;
        for (int c = 0; c < cols.Count && c < headers.Count; c++)
        {
            var header = headers[c];
            var val = cols[c].Trim();

            if (header is "entitytype" or "entity" or "type" or "mastertype") dto.EntityType = val;
            else if (header is "name" or "entityname" or "itemname") dto.Name = val;
            else if (header is "code" or "itemcode" or "vendorcode" or "modelcode") dto.Code = val;
            else if (header is "mobile" or "phone" or "mobilenumber" or "phonenumber") dto.Mobile = val;
            else if (header is "email" or "emailaddress") dto.Email = val;
            else if (header is "brand" or "brandname" or "make") dto.Brand = val;
            else if (header is "producttype" or "typename" or "category" or "productcategory") dto.ProductType = val;
            else if (header is "description" or "desc" or "details" or "notes") dto.Description = val;
            else if (header is "address" or "vendoraddress") dto.Address = val;
        }
    }


    private static string EscapeXml(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        var sb = new StringBuilder(val.Length);
        foreach (char ch in val)
        {
            if (ch == 0x9 || ch == 0xA || ch == 0xD || (ch >= 0x20 && ch <= 0xD7FF) || (ch >= 0xE000 && ch <= 0xFFFD))
            {
                sb.Append(ch);
            }
        }
        return SecurityElement.Escape(sb.ToString()) ?? "";
    }

    private static byte[] BuildClosedXmlWorkbook(List<(string SheetName, List<string> Headers, List<List<string>> Rows)> sheets)
    {
        using var memoryStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            foreach (var (sheetName, headers, rows) in sheets)
            {
                var ws = workbook.Worksheets.Add(sheetName);

                // 1. Header Row (row 1)
                for (int col = 0; col < headers.Count; col++)
                {
                    var headerCell = ws.Cell(1, col + 1);
                    headerCell.Value = headers[col];
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Font.FontSize = 11;
                    headerCell.Style.Font.FontName = "Calibri";
                    headerCell.Style.Font.FontColor = XLColor.White;
                    headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E40AF");
                    headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    headerCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }
                ws.Row(1).Height = 22;

                // 2. Data Rows
                for (int row = 0; row < rows.Count; row++)
                {
                    var rowData = rows[row];
                    int rowNum = row + 2;
                    ws.Row(rowNum).Height = 18;

                    for (int col = 0; col < rowData.Count; col++)
                    {
                        var dataCell = ws.Cell(rowNum, col + 1);
                        var rawVal = rowData[col] ?? "";
                        dataCell.Value = rawVal;
                        dataCell.Style.Font.FontSize = 10;
                        dataCell.Style.Font.FontName = "Calibri";
                        dataCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }
                }

                // 3. Set standard column widths (20 characters)
                for (int col = 1; col <= headers.Count; col++)
                {
                    ws.Column(col).Width = 22;
                }
            }

            workbook.SaveAs(memoryStream);
        } // workbook.Dispose() runs here, finalizing and closing the OpenXML zip package!

        return memoryStream.ToArray();
    }

    private static List<MasterImportRowDto> ParseClosedXmlSpreadsheet(byte[] bytes)
    {
        var rows = new List<MasterImportRowDto>();
        try
        {
            using var ms = new MemoryStream(bytes);
            using var workbook = new XLWorkbook(ms);

            foreach (var ws in workbook.Worksheets)
            {
                var defaultEntityType = NormalizeEntityType(ws.Name);
                var firstRow = ws.FirstRowUsed();
                var lastRow = ws.LastRowUsed();
                if (firstRow == null || lastRow == null) continue;

                int headerRowNum = firstRow.RowNumber();
                if (lastRow.RowNumber() <= headerRowNum) continue;

                var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
                if (lastCol <= 0) continue;

                var headers = new List<string>();
                for (int c = 1; c <= lastCol; c++)
                {
                    headers.Add(GetCellStringValue(ws.Cell(headerRowNum, c)).ToLowerInvariant());
                }

                for (int r = headerRowNum + 1; r <= lastRow.RowNumber(); r++)
                {
                    var cellValues = new List<string>();
                    for (int c = 1; c <= lastCol; c++)
                    {
                        cellValues.Add(GetCellStringValue(ws.Cell(r, c)));
                    }

                    if (cellValues.All(string.IsNullOrWhiteSpace)) continue;

                    var dto = new MasterImportRowDto { EntityType = defaultEntityType };
                    MapRowFields(dto, headers, cellValues);
                    if (string.IsNullOrWhiteSpace(dto.EntityType)) dto.EntityType = defaultEntityType;

                    if (!string.IsNullOrWhiteSpace(dto.Name) || !string.IsNullOrWhiteSpace(dto.EntityType))
                    {
                        rows.Add(dto);
                    }
                }
            }
        }
        catch
        {
            // fallback
        }
        return rows;
    }

    private static string GetCellStringValue(IXLCell? cell)
    {
        if (cell == null || cell.IsEmpty()) return "";
        try
        {
            return cell.Value.ToString()?.Trim() ?? "";
        }
        catch
        {
            return cell.GetFormattedString().Trim();
        }
    }

    private static string GetColumnName(int index)
    {
        string name = "";
        while (index > 0)
        {
            int rem = (index - 1) % 26;
            name = (char)('A' + rem) + name;
            index = (index - 1) / 26;
        }
        return name;
    }

    private static string NormalizeEntityType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var clean = raw.Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();

        return clean switch
        {
            "vendor" or "vendors" or "supplier" => "Vendor",
            "brand" or "brands" or "make" => "Brand",
            "producttype" or "producttypes" or "type" or "types" => "ProductType",
            "model" or "models" or "productmodel" or "productmodels" or "category" => "Model",
            "variant" or "variants" or "storage" or "spec" => "Variant",
            "color" or "colors" or "colour" or "colours" => "Color",
            _ => raw.Trim()
        };
    }

    private static MasterImportRowDto DeserializeRowData(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<MasterImportRowDto>(json, JsonOpts) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static MasterImportBatchSummaryDto ToBatchSummaryDto(
        MasterImportBatch batch,
        IReadOnlyList<MasterImportStagingRow>? rows)
    {
        var rowDtos = rows?.Select(r =>
        {
            var raw = DeserializeRowData(r.RawDataJson);
            return new MasterImportStagingRowDto(
                Id: r.Id,
                BatchId: r.BatchId,
                RowIndex: r.RowIndex,
                EntityType: r.EntityType,
                Action: r.Action.ToString(),
                Status: r.Status.ToString(),
                EntityKey: r.EntityKey,
                EntityName: r.EntityName,
                ValidationErrors: r.ValidationErrors,
                RawData: raw,
                IsApproved: r.IsApproved,
                IsImported: r.IsImported,
                ImportedAt: r.ImportedAt,
                ImportMessage: r.ImportMessage,
                MatchedRecord: raw.MatchedRecord);
        }).ToList();

        return new MasterImportBatchSummaryDto(
            Id: batch.Id,
            BatchNumber: batch.BatchNumber,
            FileName: batch.FileName,
            FileType: batch.FileType,
            Status: batch.Status.ToString(),
            TotalRows: batch.TotalRows,
            ValidRows: batch.ValidRows,
            ErrorRows: batch.ErrorRows,
            WarningRows: batch.WarningRows,
            CreatedCount: batch.CreatedCount,
            UpdatedCount: batch.UpdatedCount,
            SummaryJson: batch.SummaryJson,
            CreatedAt: batch.CreatedAt,
            CreatedBy: batch.CreatedBy,
            ReviewedAt: batch.ReviewedAt,
            ReviewedBy: batch.ReviewedBy,
            ReviewNotes: batch.ReviewNotes,
            Rows: rowDtos);
    }
}

