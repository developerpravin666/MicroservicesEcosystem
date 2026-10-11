using BuildingBlocks.Application.Exceptions;
using MediatR;

namespace AccountingInventory.Application.Users.Commands.Register;

/// <summary>Legacy registration is disabled; owner/staff activation requires a verified email invitation.</summary>
public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    public Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
        => Task.FromException<RegisterResult>(new ForbiddenException(
            "Direct registration has been replaced by verified email invitations. Ask your operator or tenant owner to send an invitation."));
}
