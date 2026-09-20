// ASP.NET Core Integration Example
// 
// This example shows how to integrate Zenmanage feature flags into an ASP.NET Core
// application using dependency injection and request-scoped context middleware.
//
// NuGet packages required:
//   dotnet add package Zenmanage.AspNetCore

// ────────────────────────────────────────────
// Program.cs / Startup
// ────────────────────────────────────────────

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Zenmanage;
using Zenmanage.AspNetCore;
using Zenmanage.Contexting;
using Zenmanage.Flagging;

var builder = WebApplication.CreateBuilder(args);

// Register Zenmanage with the DI container.
// ConfigBuilder reads from ZENMANAGE_ENVIRONMENT_TOKEN env var by default.
builder.Services.AddZenmanage(options =>
    options
        .WithEnvironmentToken(builder.Configuration["Zenmanage:EnvironmentToken"]
            ?? throw new InvalidOperationException("Zenmanage:EnvironmentToken is required"))
        .WithCacheBackend(CacheBackend.Memory)
        .WithCacheTtl(300));

builder.Services.AddControllers();

var app = builder.Build();

// Add Zenmanage middleware AFTER authentication so HttpContext.User is populated.
// The context factory maps the authenticated user to a Zenmanage Context.
app.UseAuthentication();

app.UseZenmanage(ctx =>
{
    var userId = ctx.User.Identity?.Name ?? "anonymous";
    var context = Context.Single("user", userId);

    // Optionally enrich with claims or request attributes.
    if (ctx.User.IsInRole("beta-tester"))
    {
        context.AddAttribute(new Attribute("role", new[] { "beta-tester" }));
    }

    return context;
});

app.UseRouting();
app.MapControllers();

app.Run();

// ────────────────────────────────────────────
// ExampleController.cs
// ────────────────────────────────────────────

// Inject the scoped IFlagManager — it automatically uses the context built
// by ZenmanageMiddleware for the current request.

/*
[ApiController]
[Route("[controller]")]
public class ExampleController : ControllerBase
{
    private readonly IFlagManager _flags;

    public ExampleController(IFlagManager flags)
    {
        _flags = flags;
    }

    [HttpGet("feature-check")]
    public async Task<IActionResult> CheckFeature()
    {
        var flag = await _flags.SingleAsync("new-checkout-flow");

        return Ok(new
        {
            flagKey    = flag.Key,
            isEnabled  = flag.IsEnabled(),
            value      = flag.AsString(),
        });
    }
}
*/

// ────────────────────────────────────────────
// Minimal API equivalent
// ────────────────────────────────────────────

app.MapGet("/feature-check", async (IFlagManager flags) =>
{
    var flag = await flags.SingleAsync("new-checkout-flow");
    return Results.Ok(new
    {
        flagKey   = flag.Key,
        isEnabled = flag.IsEnabled(),
        value     = flag.AsString(),
    });
});
