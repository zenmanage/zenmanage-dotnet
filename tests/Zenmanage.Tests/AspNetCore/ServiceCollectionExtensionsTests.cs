using Microsoft.Extensions.DependencyInjection;
using Zenmanage.AspNetCore;
using Zenmanage.Flagging;

namespace Zenmanage.Tests.AspNetCore;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddZenmanage_WithConfig_RegistersIZenmanageClientAsSingleton()
    {
        var services = new ServiceCollection();
        var config = new Config("srv_test_token");

        services.AddZenmanage(config);
        var provider = services.BuildServiceProvider();

        var client1 = provider.GetRequiredService<IZenmanageClient>();
        var client2 = provider.GetRequiredService<IZenmanageClient>();

        Assert.NotNull(client1);
        Assert.Same(client1, client2);
    }

    [Fact]
    public void AddZenmanage_WithConfig_RegistersIFlagManagerAsScoped()
    {
        var services = new ServiceCollection();
        var config = new Config("srv_test_token");

        services.AddZenmanage(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IFlagManager));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
    }

    [Fact]
    public void AddZenmanage_WithBuilder_RegistersIZenmanageClientAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddZenmanage(b => b.WithEnvironmentToken("srv_test_token"));
        var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IZenmanageClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddZenmanage_WithBuilder_IFlagManagerResolvesFromScope()
    {
        var services = new ServiceCollection();
        services.AddZenmanage(b => b.WithEnvironmentToken("srv_test_token"));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var flags = scope.ServiceProvider.GetRequiredService<IFlagManager>();
        Assert.NotNull(flags);
    }

    [Fact]
    public void AddZenmanage_CalledTwice_DoesNotRegisterDuplicates()
    {
        var services = new ServiceCollection();
        var config = new Config("srv_test_token");

        services.AddZenmanage(config);
        services.AddZenmanage(config);

        var clientRegistrations = services.Where(d => d.ServiceType == typeof(IZenmanageClient)).ToList();
        Assert.Single(clientRegistrations);
    }

    [Fact]
    public void AddZenmanage_IFlagManagerWithinSameScope_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddZenmanage(new Config("srv_test_token"));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var flags1 = scope.ServiceProvider.GetRequiredService<IFlagManager>();
        var flags2 = scope.ServiceProvider.GetRequiredService<IFlagManager>();

        // Within the same scope the same instance should be returned
        Assert.Same(flags1, flags2);
    }
}
