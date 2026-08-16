using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Zenmanage.Contexting;
using Zenmanage.Flagging;

namespace Zenmanage.AspNetCore;

/// <summary>
/// Extension methods for registering Zenmanage services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Zenmanage client and a request-scoped <see cref="IFlagManager"/> using the provided <see cref="Config"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The Zenmanage configuration.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddZenmanage(this IServiceCollection services, Config config)
    {
        services.TryAddSingleton<IZenmanageClient>(new Zenmanage(config));
        RegisterScopedFlagManager(services);
        return services;
    }

    /// <summary>
    /// Registers the Zenmanage client and a request-scoped <see cref="IFlagManager"/> using a builder delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate that configures a <see cref="ConfigBuilder"/>.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddZenmanage(this IServiceCollection services, Action<ConfigBuilder> configure)
    {
        services.TryAddSingleton<IZenmanageClient>(sp =>
        {
            var builder = ConfigBuilder.Create();
            configure(builder);
            var logger = sp.GetService<ILogger<Zenmanage>>();
            if (logger is not null)
            {
                builder.WithLogger(logger);
            }

            return new Zenmanage(builder.Build());
        });
        RegisterScopedFlagManager(services);
        return services;
    }

    private static void RegisterScopedFlagManager(IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        // Register a scoped IFlagManager that automatically picks up request context
        // set by ZenmanageMiddleware.
        services.TryAddScoped<IFlagManager>(sp =>
        {
            var client = sp.GetRequiredService<IZenmanageClient>();
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var flags = client.Flags();

            if (httpContextAccessor?.HttpContext?.Items[ZenmanageMiddleware.ContextItemKey] is Context ctx)
            {
                return flags.WithContext(ctx);
            }

            return flags;
        });
    }
}
