using System.Text.Json;
using AccountingInventory.Application.MasterImport;
using BuildingBlocks.WebDefaults;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;

// Template generation does not access the database.
var service = new MasterDataImportService(null!, NullLogger<MasterDataImportService>.Instance);
var workbookBytes = service.GenerateMultiSheetExcelTemplateXlsx();
const string excelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

foreach (var path in new[] { "/api/masters/template", "/api/masters/export",
    "/api/admin/tenants/test/masters/import/template", "/api/admin/tenants/test/masters/import/export" })
{
    var context = await Run(path, async ctx =>
    {
        ctx.Response.ContentType = excelType;
        ctx.Response.ContentLength = workbookBytes.Length;
        ctx.Response.Headers.ContentDisposition = "attachment; filename=siddhi_master_import_template.xlsx";
        await ctx.Response.Body.WriteAsync(workbookBytes);
    });
    var downloaded = ((MemoryStream)context.Response.Body).ToArray();
    Check(downloaded.SequenceEqual(workbookBytes), $"{path}: workbook bytes changed");
    Check(context.Response.ContentType == excelType, $"{path}: content type changed");
    Check(context.Response.ContentLength == workbookBytes.Length, $"{path}: length changed");
    Check(context.Response.Headers.ContentDisposition.ToString().Contains(".xlsx"), $"{path}: filename lost");
    using var stream = new MemoryStream(downloaded);
    using var workbook = new XLWorkbook(stream);
    Check(workbook.Worksheets.Select(ws => ws.Name).SequenceEqual(
        new[] { "Vendors", "Brands", "ProductTypes", "Models", "Variants", "Colors" }), "Workbook sheets changed");
    Check(workbook.Worksheet("Vendors").Cell("C2").GetString() == "9876543210", "Mobile number changed");
}

var binary = await Run("/api/file", async ctx =>
{
    ctx.Response.ContentType = "application/octet-stream";
    await ctx.Response.Body.WriteAsync(workbookBytes);
});
Check(((MemoryStream)binary.Response.Body).ToArray().SequenceEqual(workbookBytes), "Binary response changed");

var json = await Run("/api/masters", ctx => ctx.Response.WriteAsJsonAsync(new { count = 6 }));
using (var body = JsonDocument.Parse(((MemoryStream)json.Response.Body).ToArray()))
{
    Check(body.RootElement.GetProperty("success").GetBoolean(), "JSON success envelope lost");
    Check(body.RootElement.GetProperty("data").GetProperty("count").GetInt32() == 6, "JSON data changed");
}
var error = await Run("/api/masters/template", ctx =>
{
    ctx.Response.StatusCode = 400;
    return ctx.Response.WriteAsJsonAsync(new { message = "Cannot export masters" });
});
using (var body = JsonDocument.Parse(((MemoryStream)error.Response.Body).ToArray()))
{
    Check(!body.RootElement.GetProperty("success").GetBoolean(), "Error envelope lost");
    Check(body.RootElement.GetProperty("message").GetString() == "Cannot export masters", "Error message lost");
}
Console.WriteLine("PASS: four master download paths preserve readable XLSX bytes; binary, JSON and error responses verified.");

static async Task<DefaultHttpContext> Run(string path, RequestDelegate next)
{
    var context = new DefaultHttpContext();
    context.Request.Path = path;
    context.Response.Body = new MemoryStream();
    await new ApiResponseEnvelopeMiddleware(next).InvokeAsync(context);
    return context;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
