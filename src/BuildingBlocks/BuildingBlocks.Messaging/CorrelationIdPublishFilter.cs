using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.Security;
using MassTransit;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Messaging;

/// <summary>
/// MassTransit publish filter that copies the current HTTP request's tenant and
/// correlation identifiers onto every outbound message header, so a trace — and the
/// tenant it belongs to — can be followed from the originating API call through
/// RabbitMQ into any consumer.
/// </summary>
public sealed class CorrelationIdPublishFilter<T>(IHttpContextAccessor httpContextAccessor, ITenantContext tenantContext)
    : IFilter<PublishContext<T>> where T : class
{
    public const string CorrelationHeaderName = "X-Correlation-ID";

    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        if (httpContextAccessor.HttpContext?.Items[CorrelationHeaderName] is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            context.Headers.Set(CorrelationHeaderName, correlationId);
        }

        if (tenantContext.TenantId is { } tenantId)
        {
            context.Headers.Set(TenantClaimTypes.TenantId, tenantId.ToString());
        }

        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlationIdPublishFilter");
}
