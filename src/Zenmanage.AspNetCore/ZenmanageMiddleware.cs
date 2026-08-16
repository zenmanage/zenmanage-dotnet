using Microsoft.AspNetCore.Http;
using Zenmanage.Contexting;

namespace Zenmanage.AspNetCore;

/// <summary>
/// ASP.NET Core middleware that extracts a Zenmanage <see cref="Context"/> from the current
/// <see cref="HttpContext"/> and stores it for use by the scoped <c>IFlagManager</c>.
/// </summary>
/// <remarks>
/// Add this middleware with <c>app.UseZenmanage()</c> after authentication middleware so that
/// <see cref="HttpContext.User"/> claims are available. The extracted context is stored in
/// <see cref="HttpContext.Items"/> under <see cref="ContextItemKey"/> and is automatically
/// consumed by the scoped <c>IFlagManager</c> registered via
/// <see cref="ServiceCollectionExtensions.AddZenmanage(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{ConfigBuilder})"/>.
/// </remarks>
public sealed class ZenmanageMiddleware
{
    /// <summary>
    /// The key used to store the <see cref="Context"/> in <see cref="HttpContext.Items"/>.
    /// </summary>
    public const string ContextItemKey = "Zenmanage.Context";

    private readonly RequestDelegate next;
    private readonly Func<HttpContext, Context?> contextFactory;

    /// <summary>
    /// Initializes the middleware with a custom context factory.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="contextFactory">
    /// A delegate that builds a <see cref="Context"/> from the incoming <see cref="HttpContext"/>.
    /// Return <c>null</c> to skip context injection for the request.
    /// </param>
    public ZenmanageMiddleware(RequestDelegate next, Func<HttpContext, Context?> contextFactory)
    {
        this.next = next;
        this.contextFactory = contextFactory;
    }

    /// <summary>
    /// Executes the middleware, building a context from the HTTP request and storing it for downstream use.
    /// </summary>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var context = contextFactory(httpContext);
        if (context is not null)
        {
            httpContext.Items[ContextItemKey] = context;
        }

        await next(httpContext).ConfigureAwait(false);
    }
}
