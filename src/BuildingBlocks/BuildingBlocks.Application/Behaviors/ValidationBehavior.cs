using FluentValidation;
using MediatR;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs every registered FluentValidation validator for
/// a request before its handler executes. On failure it throws FluentValidation's
/// ValidationException, which the shared GlobalExceptionHandler (BuildingBlocks.
/// WebDefaults) converts into an RFC 7807 ProblemDetails response — handlers never
/// need try/catch for this. One implementation, shared by every service's MediatR
/// pipeline (Identity, Inventory, Tenant, and any Gold-Master clone).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
