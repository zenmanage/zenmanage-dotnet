using Microsoft.AspNetCore.Builder;
using Zenmanage.Contexting;

namespace Zenmanage.AspNetCore;

/// <summary>
/// Extension methods for adding Zenmanage middleware to the request pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="ZenmanageMiddleware"/> to the pipeline using a custom context factory.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="contextFactory">
    /// A delegate that builds a Zenmanage <see cref="Context"/> from the current <see cref="Microsoft.AspNetCore.Http.HttpContext"/>.
    /// The extracted context is used by the scoped <c>IFlagManager</c> for flag evaluation.
    /// </param>
    /// <returns>The modified application builder.</returns>
    /// <example>
    /// <code>
    /// app.UseZenmanage(ctx =>
    ///     Context.Single(ctx.User.Identity?.Name ?? "anonymous"));
    /// </code>
    /// </example>
    public static IApplicationBuilder UseZenmanage(
        this IApplicationBuilder app,
        Func<Microsoft.AspNetCore.Http.HttpContext, Context?> contextFactory)
    {
        return app.UseMiddleware<ZenmanageMiddleware>(contextFactory);
    }

    /// <summary>
    /// Adds the <see cref="ZenmanageMiddleware"/> to the pipeline, automatically deriving the
    /// context identifier from <c>HttpContext.User.Identity.Name</c>. Falls back to <c>"anonymous"</c>
    /// when no authenticated user is present.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The modified application builder.</returns>
    public static IApplicationBuilder UseZenmanage(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ZenmanageMiddleware>(
            (Func<Microsoft.AspNetCore.Http.HttpContext, Context?>)(ctx =>
                Context.Single("user", ctx.User.Identity?.Name ?? "anonymous")));
    }
}
