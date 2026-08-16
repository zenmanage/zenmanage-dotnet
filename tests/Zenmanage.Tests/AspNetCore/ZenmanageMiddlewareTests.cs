using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zenmanage.AspNetCore;
using Zenmanage.Contexting;
using Zenmanage.Flagging;

namespace Zenmanage.Tests.AspNetCore;

public class ZenmanageMiddlewareTests
{
    [Fact]
    public async Task Middleware_SetsContextInHttpItems_WhenFactoryReturnsContext()
    {
        Context? capturedContext = null;

        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddZenmanage(new Config("srv_test_token"));
                });
                web.Configure(app =>
                {
                    app.UseZenmanage(_ => Context.Single("user", "user-123"));
                    app.Run(ctx =>
                    {
                        capturedContext = ctx.Items[ZenmanageMiddleware.ContextItemKey] as Context;
                        return Task.CompletedTask;
                    });
                });
            })
            .StartAsync();

        await host.GetTestClient().GetAsync("/");

        Assert.NotNull(capturedContext);
        Assert.Equal("user-123", capturedContext!.GetId());
        Assert.Equal("user", capturedContext!.Type);
    }

    [Fact]
    public async Task Middleware_SkipsContextInjection_WhenFactoryReturnsNull()
    {
        bool contextKeyPresent = false;

        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddZenmanage(new Config("srv_test_token"));
                });
                web.Configure(app =>
                {
                    app.UseZenmanage(_ => null);
                    app.Run(ctx =>
                    {
                        contextKeyPresent = ctx.Items.ContainsKey(ZenmanageMiddleware.ContextItemKey);
                        return Task.CompletedTask;
                    });
                });
            })
            .StartAsync();

        await host.GetTestClient().GetAsync("/");

        Assert.False(contextKeyPresent);
    }

    [Fact]
    public async Task Middleware_DefaultOverload_UsesAnonymousWhenNoUser()
    {
        Context? capturedContext = null;

        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddZenmanage(new Config("srv_test_token"));
                });
                web.Configure(app =>
                {
                    app.UseZenmanage(); // default overload
                    app.Run(ctx =>
                    {
                        capturedContext = ctx.Items[ZenmanageMiddleware.ContextItemKey] as Context;
                        return Task.CompletedTask;
                    });
                });
            })
            .StartAsync();

        await host.GetTestClient().GetAsync("/");

        Assert.NotNull(capturedContext);
        Assert.Equal("anonymous", capturedContext!.GetId());
    }

    [Fact]
    public async Task ScopedFlagManager_UsesContextFromMiddleware()
    {
        IFlagManager? resolvedFlags = null;

        using var host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddZenmanage(new Config("srv_test_token"));
                });
                web.Configure(app =>
                {
                    app.UseZenmanage(_ => Context.Single("user", "user-456"));
                    app.Run(ctx =>
                    {
                        resolvedFlags = ctx.RequestServices.GetRequiredService<IFlagManager>();
                        return Task.CompletedTask;
                    });
                });
            })
            .StartAsync();

        await host.GetTestClient().GetAsync("/");

        Assert.NotNull(resolvedFlags);
    }
}
