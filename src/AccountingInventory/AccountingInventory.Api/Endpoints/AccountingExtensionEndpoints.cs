using System.Security.Claims;
using System.Text.Json;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Application.Inventory;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class AccountingExtensionEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static RouteGroupBuilder MapAccountingExtensionEndpoints(this IEndpointRouteBuilder app)
    {
        var approvals = app.MapGroup("/api/accounting-approvals").WithTags("Accounting Approvals")
            .RequireAuthorization("AuthenticatedUser").AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());
        approvals.MapGet("", async ([FromQuery] AccountingInventory.Domain.Entities.ApprovalStatus? status, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetAccountingApprovalsQuery(status), ct))).WithName("GetAccountingApprovals");
        approvals.MapPost("", async (SubmitApprovalRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
            Results.Created("/api/accounting-approvals", await sender.Send(new SubmitAccountingApprovalCommand(request.Action,
                request.ResourceId, request.Summary, request.Payload.GetRawText(), UserId(http)), ct))).WithName("SubmitAccountingApproval");
        approvals.MapPost("/{id:guid}/decision", async (Guid id, DecideApprovalRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new DecideAccountingApprovalCommand(id, request.Approve, request.Note, UserId(http)), ct)))
            .WithName("DecideAccountingApproval");
        approvals.MapPost("/{id:guid}/apply", async (Guid id, IAccountingInventoryDbContext db, ISender sender, CancellationToken ct) =>
        {
            var approval = await db.AccountingApprovals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (approval is null) return Results.NotFound();
            if (approval.Status != AccountingInventory.Domain.Entities.ApprovalStatus.Approved) return Results.Conflict(new { message = "Only approved requests can be applied." });
            object? result = approval.Action switch
            {
                AccountingInventory.Domain.Entities.ApprovalAction.Journal => await ApplyJournal(approval.PayloadJson, id, sender, ct),
                AccountingInventory.Domain.Entities.ApprovalAction.FinancialCorrection => await ApplyCorrection(approval.PayloadJson, id, sender, ct),
                AccountingInventory.Domain.Entities.ApprovalAction.ClosePeriod => await ApplyClose(approval.PayloadJson, id, sender, ct),
                AccountingInventory.Domain.Entities.ApprovalAction.InventoryWriteOff => await ApplyWriteOff(approval.PayloadJson, id, sender, ct),
                _ => null
            };
            return result is null ? Results.BadRequest(new { message = "Approval action is not supported." }) : Results.Ok(result);
        }).WithName("ApplyAccountingApproval");

        var permissions = app.MapGroup("/api/accounting-permissions").WithTags("Accounting Permissions")
            .RequireAuthorization("AuthenticatedUser").AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());
        permissions.MapGet("", async (HttpContext http, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetAccountingPermissionsQuery(UserId(http)), ct)));
        permissions.MapPut("/{userId:guid}/{permissionCode}", async (Guid userId, string permissionCode,
            SetAccountingPermissionRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SetAccountingPermissionCommand(userId, permissionCode, request.Granted, UserId(http)), ct);
            return Results.NoContent();
        });

        var dimensions = app.MapGroup("/api/accounting-dimensions").WithTags("Accounting Dimensions")
            .RequireAuthorization("AuthenticatedUser").AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());
        dimensions.MapGet("", async (ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetAccountingDimensionsQuery(), ct)));
        dimensions.MapPost("", async (CreateAccountingDimensionCommand command, HttpContext http, ISender sender, CancellationToken ct) =>
            Results.Created("/api/accounting-dimensions", await sender.Send(command with { ActorId = UserId(http) }, ct)));

        var documents = app.MapGroup("/api/supporting-documents").WithTags("Supporting Documents")
            .RequireAuthorization("AuthenticatedUser").AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());
        documents.MapGet("", async ([FromQuery] string resourceType, [FromQuery] string resourceId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSupportingDocumentsQuery(resourceType, resourceId), ct)));
        documents.MapPost("", async (AddDocumentRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
            Results.Created("/api/supporting-documents", await sender.Send(new AddSupportingDocumentCommand(request.ResourceType,
                request.ResourceId, request.FileName, request.ContentType, request.StorageReference, request.Description, UserId(http)), ct)));

        var assets = app.MapGroup("/api/fixed-assets").WithTags("Fixed Assets")
            .RequireAuthorization("AuthenticatedUser").AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());
        assets.MapGet("", async (ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetFixedAssetsQuery(), ct)));
        assets.MapPost("", async (AcquireFixedAssetCommand command, HttpContext http, ISender sender, CancellationToken ct) =>
            Results.Created("/api/fixed-assets", await sender.Send(command with { ActorId = UserId(http) }, ct)));
        assets.MapPost("/{id:guid}/depreciate", async (Guid id, DepreciateAssetRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new DepreciateFixedAssetCommand(id, request.DepreciationDate, request.Months, UserId(http)), ct)));
        assets.MapPost("/{id:guid}/dispose", async (Guid id, DisposeAssetRequest request, HttpContext http, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new DisposeFixedAssetCommand(id, request.DisposalDate, request.Proceeds, request.ProceedsAccountId, UserId(http)), ct)));

        var budgets = app.MapGroup("/api/account-budgets").WithTags("Budgets and Variance")
            .RequireAuthorization("AuthenticatedUser").AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());
        budgets.MapGet("", async ([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetBudgetVarianceQuery(from, to), ct)));
        budgets.MapPost("", async (CreateAccountBudgetCommand command, HttpContext http, ISender sender, CancellationToken ct) =>
            Results.Created("/api/account-budgets", await sender.Send(command with { ActorId = UserId(http) }, ct)));
        return approvals;
    }

    private static Guid? UserId(HttpContext http) => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static async Task<object> ApplyJournal(string json, Guid id, ISender sender, CancellationToken ct)
        => await sender.Send((JsonSerializer.Deserialize<PostJournalCommand>(json, JsonOptions) ?? throw new JsonException()) with { ApprovalId = id }, ct);
    private static async Task<object> ApplyCorrection(string json, Guid id, ISender sender, CancellationToken ct)
    {
        var command = JsonSerializer.Deserialize<PostJournalCommand>(json, JsonOptions) ?? throw new JsonException();
        if (!string.Equals(command.SourceType, "FinancialCorrection", StringComparison.OrdinalIgnoreCase)) return new { message = "Correction payload must use sourceType FinancialCorrection." };
        return await sender.Send(command with { ApprovalId = id }, ct);
    }
    private static async Task<object> ApplyClose(string json, Guid id, ISender sender, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(json); var periodId = doc.RootElement.GetProperty("id").GetGuid();
        return await sender.Send(new CloseAccountingPeriodCommand(periodId, id), ct);
    }
    private static async Task<object> ApplyWriteOff(string json, Guid id, ISender sender, CancellationToken ct)
    {
        var command = JsonSerializer.Deserialize<WriteOffInventoryCommand>(json, JsonOptions) ?? throw new JsonException();
        return new { id = await sender.Send(command with { ApprovalId = id }, ct) };
    }
}

public sealed record SubmitApprovalRequest(AccountingInventory.Domain.Entities.ApprovalAction Action,
    string ResourceId, string Summary, JsonElement Payload);
public sealed record DecideApprovalRequest(bool Approve, string? Note);
public sealed record SetAccountingPermissionRequest(bool Granted);
public sealed record AddDocumentRequest(string ResourceType, string ResourceId, string FileName,
    string ContentType, string StorageReference, string? Description);
public sealed record DepreciateAssetRequest(DateOnly DepreciationDate, int Months = 1);
public sealed record DisposeAssetRequest(DateOnly DisposalDate, decimal Proceeds, Guid ProceedsAccountId);
