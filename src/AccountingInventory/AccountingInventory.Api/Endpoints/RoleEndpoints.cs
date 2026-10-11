using BuildingBlocks.Security;
using AccountingInventory.Application.Roles.Commands.CreateRole;
using AccountingInventory.Application.Roles.Commands.DeleteRole;
using AccountingInventory.Application.Roles.Commands.UpdateRole;
using AccountingInventory.Application.Roles.Queries.GetRoles;
using AccountingInventory.Application.Users.Commands.AssignRole;
using AccountingInventory.Application.Users.Commands.RemoveRole;
using MediatR;

namespace AccountingInventory.Api.Endpoints;

/// <summary>
/// This IS "roles and controller access managed from the database": every mutation
/// here edits rows in the tenant's own Roles table, and the effect shows up in every
/// affected user's JWT the next time they log in or refresh (typically within 15
/// minutes) — no code change, no redeploy, in this service or any other.
/// </summary>
public static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles")
            .WithTags("Roles")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>();

        group.MapPost("/", async (CreateRoleCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/roles/{result.RoleId}", result);
            })
            .WithName("CreateRole").RequirePermission("Roles.Manage");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetRolesQuery(), ct)))
            .WithName("GetRoles").RequirePermission("Roles.Manage");

        group.MapPut("/{id:guid}/permissions", async (Guid id, UpdateRolePermissionsBody body, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new UpdateRoleCommand(id, body.PermissionCodes), ct)))
            .WithName("UpdateRolePermissions").RequirePermission("Roles.Manage");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteRoleCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("DeleteRole").RequirePermission("Roles.Manage");

        group.MapPost("/{roleId:guid}/users/{userId:guid}", async (Guid roleId, Guid userId, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new AssignRoleCommand(userId, roleId), ct);
                return Results.NoContent();
            })
            .WithName("AssignRoleToUser").RequirePermission("Users.Manage");

        group.MapDelete("/{roleId:guid}/users/{userId:guid}", async (Guid roleId, Guid userId, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new RemoveRoleCommand(userId, roleId), ct);
                return Results.NoContent();
            })
            .WithName("RemoveRoleFromUser").RequirePermission("Users.Manage");

        return group;
    }
}

public sealed record UpdateRolePermissionsBody(IReadOnlyList<string> PermissionCodes);
