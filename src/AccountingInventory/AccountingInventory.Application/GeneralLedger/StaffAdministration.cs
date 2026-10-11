using System.Security.Cryptography;
using System.Text;
using System.Net;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain.MultiTenancy;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AccountingInventory.Application.GeneralLedger;

public sealed record InviteStaffCommand(string UserName, string Email, string Mobile, bool BootstrapOwner = false)
    : IRequest<Guid>, IBusinessPermissionRequest { public string BusinessPermission => ""; }
public sealed record AcceptStaffInvitationCommand(string Token, string Password) : IRequest, IBusinessPermissionRequest
{ public string BusinessPermission => ""; }
public sealed record SetStaffStatusCommand(Guid UserId, bool Active) : IRequest, IBusinessPermissionRequest
{ public string BusinessPermission => ""; }
public sealed record TransferOwnerCommand(Guid UserId) : IRequest, IBusinessPermissionRequest
{ public string BusinessPermission => ""; }
public sealed record GetMyAccountingAccessQuery() : IRequest<AccountingAccessSummary>;
public sealed record AccountingAccessSummary(bool IsOwner, IReadOnlyList<string> Permissions);
public sealed record GetStaffQuery() : IRequest<IReadOnlyList<StaffSummary>>;
public sealed record StaffSummary(Guid Id, string UserName, string Email, string Mobile, bool IsOwner, bool IsActive, bool EmailVerified);

public sealed class StaffAdministrationHandler(IAccountingInventoryDbContext db, IRequestIdentity actor, ITenantContext tenant,
    IEmailSender email, IConfiguration configuration, IPasswordHasher<User> hasher) :
    IRequestHandler<InviteStaffCommand, Guid>, IRequestHandler<AcceptStaffInvitationCommand>,
    IRequestHandler<SetStaffStatusCommand>, IRequestHandler<TransferOwnerCommand>,
    IRequestHandler<GetMyAccountingAccessQuery, AccountingAccessSummary>, IRequestHandler<GetStaffQuery, IReadOnlyList<StaffSummary>>
{
    private async Task EnsureOwner(CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == actor.UserId && x.IsOwner && x.IsActive, ct))
            throw new ForbiddenException("Only the verified tenant owner can administer staff.");
    }
    public async Task<Guid> Handle(InviteStaffCommand q, CancellationToken ct)
    {
        var address = q.Email.Trim().ToLowerInvariant();
        if (!q.BootstrapOwner) await EnsureOwner(ct);
        else if (await db.Users.IgnoreQueryFilters().AnyAsync(x => !x.IsOwner || x.EmailVerified || x.IsActive || x.Email.ToLower() != address, ct))
            throw new ConflictException("Owner bootstrap only supports an empty tenant or resending its unverified owner invitation.");
        if (!System.Net.Mail.MailAddress.TryCreate(address, out _)) throw new ConflictException("A valid email address is required.");
        var name = q.UserName.Trim();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == address, ct);
        if (user is not null && (user.EmailVerified || user.IsActive)) throw new ConflictException("This user is already registered.");
        if (user is null)
        {
            if (await db.Users.AnyAsync(x => x.UserName.ToLower() == name.ToLower(), ct)) throw new ConflictException("Username is already used.");
            user = User.Create(name, q.Mobile.Trim(), address);
            user.SetOwner(q.BootstrapOwner);
            db.Users.Add(user);
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.Invite(Hash(token), DateTimeOffset.UtcNow.AddHours(24));
        await db.SaveChangesAsync(ct);
        var baseUrl = configuration["Frontend:BaseUrl"] ?? throw new ConflictException("Frontend:BaseUrl must be configured for invitations.");
        var link = $"{baseUrl.TrimEnd('/')}/accept-invitation?tenantId={tenant.TenantId}&token={token}";
        await email.SendAsync(address, "Activate your account", $"<p>Activate your account using this link within 24 hours:</p><a href=\"{WebUtility.HtmlEncode(link)}\">Activate account</a>", ct);
        return user.Id;
    }
    public async Task Handle(AcceptStaffInvitationCommand q, CancellationToken ct)
    {
        if (q.Password.Length < 12 || !q.Password.Any(char.IsDigit) || !q.Password.Any(char.IsUpper) || !q.Password.Any(char.IsLower))
            throw new ConflictException("Use at least 12 characters including upper/lowercase letters and a number.");
        var hash = Hash(q.Token);
        var user = await db.Users.SingleOrDefaultAsync(x => x.InvitationHash == hash && x.InvitationExpiresAt > DateTimeOffset.UtcNow, ct)
            ?? throw new ForbiddenException("The invitation is invalid or expired.");
        user.SetPasswordHash(hasher.HashPassword(user, q.Password)); user.AcceptInvitation();
        await db.SaveChangesAsync(ct);
    }
    public async Task Handle(SetStaffStatusCommand q, CancellationToken ct)
    {
        await EnsureOwner(ct);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == q.UserId, ct) ?? throw new NotFoundException("Staff user was not found.");
        if (user.IsOwner || !user.EmailVerified) throw new ConflictException("Owner status cannot be changed here; invited users must verify their email first.");
        if (q.Active) user.Activate(); else { user.DeActivate(); user.RevokeRefreshToken(); }
        await db.SaveChangesAsync(ct);
    }
    public async Task Handle(TransferOwnerCommand q, CancellationToken ct)
    {
        await EnsureOwner(ct);
        var next = await db.Users.SingleOrDefaultAsync(x => x.Id == q.UserId && x.IsActive && x.EmailVerified, ct)
            ?? throw new ConflictException("Choose an active user with a verified email.");
        var current = await db.Users.SingleAsync(x => x.IsOwner, ct);
        if (next.Id == current.Id) return;
        current.SetOwner(false); await db.SaveChangesAsync(ct);
        next.SetOwner(true); await db.SaveChangesAsync(ct);
    }
    public async Task<AccountingAccessSummary> Handle(GetMyAccountingAccessQuery q, CancellationToken ct)
    {
        var owner = await db.Users.AnyAsync(x => x.Id == actor.UserId && x.IsOwner && x.IsActive, ct);
        var permissions = owner ? AccountingPermissionGate.Codes : await db.AccountingUserPermissions
            .Where(x => x.UserId == actor.UserId && x.IsActive).Select(x => x.PermissionCode).ToArrayAsync(ct);
        return new(owner, permissions);
    }
    public async Task<IReadOnlyList<StaffSummary>> Handle(GetStaffQuery q, CancellationToken ct)
    {
        await EnsureOwner(ct);
        return await db.Users.AsNoTracking().OrderBy(x => x.UserName)
            .Select(x => new StaffSummary(x.Id, x.UserName, x.Email, x.Mobile, x.IsOwner, x.IsActive, x.EmailVerified)).ToArrayAsync(ct);
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
