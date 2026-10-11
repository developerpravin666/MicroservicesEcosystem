using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BuildingBlocks.WebDefaults;

/// <summary>
/// Documents the tenant-selection header on authenticated API operations. Anonymous
/// bootstrap operations, such as tenant registration and token issuance, do not
/// receive the header because they establish a tenant themselves.
/// </summary>
public sealed class TenantIdHeaderOperationFilter : IOperationFilter
{
    private const string TenantIdHeader = "X-Tenant-Id";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata is null)
        {
            return;
        }

        var explicitlyRequiresTenantHeader = metadata.OfType<RequiresTenantIdHeaderAttribute>().Any();
        var requiresAuthenticatedTenant = !metadata.OfType<IAllowAnonymous>().Any() &&
                                          metadata.OfType<IAuthorizeData>().Any();
        if (!explicitlyRequiresTenantHeader && !requiresAuthenticatedTenant)
        {
            return;
        }

        operation.Parameters ??= [];

        if (operation.Parameters.Any(parameter =>
                parameter.In == ParameterLocation.Header &&
                string.Equals(parameter.Name, TenantIdHeader, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = TenantIdHeader,
            In = ParameterLocation.Header,
            Required = true,
            Description = "The authenticated tenant's GUID. It must match the tenant in the Bearer token.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}

/// <summary>Marks an anonymous endpoint that still selects a tenant by header.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequiresTenantIdHeaderAttribute : Attribute;
