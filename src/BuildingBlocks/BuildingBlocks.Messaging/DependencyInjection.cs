using System.Reflection;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Host-level messaging registration. Called EXACTLY ONCE per process — same rule as
/// BuildingBlocks.Security.AddPlatformSecurity, and for the same reason: MassTransit
/// expects a single bus configuration per process. A standalone service's Program.cs
/// passes just its own Application assembly (for its own consumers); a combined host
/// running several services together passes every module's Application assembly so
/// every consumer, from every service, is registered on the one shared bus.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPlatformMessaging(
        this IServiceCollection services, IConfiguration configuration, params Assembly[] consumerAssemblies)
    {
        var rabbitMqOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions { UseInMemory = true };

        services.AddScoped<IEventPublisher, EventPublisher>();

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.SetKebabCaseEndpointNameFormatter();

            foreach (var assembly in consumerAssemblies)
            {
                busConfigurator.AddConsumers(assembly);
            }

            if (rabbitMqOptions.UseInMemory ||
                string.Equals(rabbitMqOptions.Host, "in-memory", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(rabbitMqOptions.Host))
            {
                busConfigurator.UsingInMemory((context, cfg) =>
                {
                    cfg.UsePublishFilter(typeof(CorrelationIdPublishFilter<>), context);
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                busConfigurator.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitMqOptions.Host, rabbitMqOptions.VirtualHost, h =>
                    {
                        h.Username(rabbitMqOptions.Username);
                        h.Password(rabbitMqOptions.Password);
                    });

                    cfg.UsePublishFilter(typeof(CorrelationIdPublishFilter<>), context);

                    // Automatic retry with exponential backoff, then MassTransit routes
                    // the message to a per-queue dead-letter exchange/queue after
                    // exhausting retries.
                    cfg.UseMessageRetry(retry => retry.Exponential(
                        retryLimit: 5,
                        minInterval: TimeSpan.FromSeconds(1),
                        maxInterval: TimeSpan.FromSeconds(30),
                        intervalDelta: TimeSpan.FromSeconds(5)));

                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }
}
