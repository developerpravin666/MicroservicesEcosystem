using AccountingInventory.Application.MasterImport;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingInventory.Api.Endpoints;

public sealed class AdminImportStoreEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!Guid.TryParse(http.Request.RouteValues["tenantId"]?.ToString(), out var tenantId))
            return Results.BadRequest(new { message = "A valid tenant id is required." });
        var directory = http.RequestServices.GetRequiredService<TenantDbContext>();
        var tenant = await directory.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantId, http.RequestAborted);
        if (tenant is null) return Results.NotFound(new { message = "Tenant was not found." });
        // Templates contain no tenant data and do not require a provisioned store.
        if (http.Request.Path.Value?.EndsWith("/template", StringComparison.OrdinalIgnoreCase) == true)
            return await next(context);
        var ready = await directory.Database.SqlQueryRaw<bool>("""
            SELECT to_regclass(format('%I.master_import_batches', @schema)) IS NOT NULL
                AND to_regclass(format('%I.master_import_staging_rows', @schema)) IS NOT NULL AS "Value"
            """, new NpgsqlParameter("schema", tenant.SchemaName)).SingleAsync(http.RequestAborted);
        if (ready) return await next(context);
        if (!tenant.IsActive && HttpMethods.IsGet(http.Request.Method)
            && http.Request.Path.Value?.EndsWith("/batches", StringComparison.OrdinalIgnoreCase) == true)
            return Results.Ok(Array.Empty<MasterImportBatchSummaryDto>());
        return Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Tenant store is not ready for master imports.",
            detail: tenant.IsActive
                ? "Apply this tenant's database migrations from Database Migrations before using Master Data Import."
                : "Approve and provision this tenant from Store Approvals before using Master Data Import.");
    }
}
