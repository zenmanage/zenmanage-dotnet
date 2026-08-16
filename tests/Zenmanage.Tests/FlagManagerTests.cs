using Microsoft.Extensions.Logging.Abstractions;
using Zenmanage.Caching;
using Zenmanage.Contexting;
using Zenmanage.Defaults;
using Zenmanage.Exceptions;
using Zenmanage.Flagging;
using Zenmanage.Rules;

namespace Zenmanage.Tests;

public sealed class FlagManagerTests
{
    [Fact]
    public async Task SingleAsync_UsesInlineDefault_WhenFlagMissing()
    {
        var manager = CreateManager(Array.Empty<FlagData>());

        var flag = await manager.SingleAsync("missing-flag", true);

        Assert.True(flag.IsEnabled());
    }

    [Fact]
    public async Task SingleAsync_UsesDefaultsCollection_WhenFlagMissing()
    {
        var defaults = new DefaultsCollection().Add("missing-flag", "fallback");
        var manager = CreateManager(Array.Empty<FlagData>()).WithDefaults(defaults);

        var flag = await manager.SingleAsync("missing-flag");

        Assert.Equal("fallback", flag.AsString());
    }

    [Fact]
    public async Task SingleAsync_Throws_WhenFlagMissingAndNoDefaultExists()
    {
        var manager = CreateManager(Array.Empty<FlagData>());

        var exception = await Assert.ThrowsAsync<EvaluationException>(() => manager.SingleAsync("missing-flag"));

        Assert.Equal("Flag not found: missing-flag", exception.Message);
    }

    [Fact]
    public async Task AllAsync_EvaluatesRulesAgainstContext()
    {
        var flagData = new FlagData(
            "1",
            FlagType.String,
            "checkout-flow",
            "Checkout Flow",
            TestData.StringTarget("fallback"),
            new[] { TestData.Rule("country", "equal", "US", "one-page") });

        var manager = CreateManager(flagData).WithContext(new Context("user", identifier: "user-1", attributes: new[]
        {
            new ContextAttribute("country", new[] { "US" })
        }));

        var flags = await manager.AllAsync();

        Assert.Single(flags);
        Assert.Equal("one-page", flags[0].AsString());
    }

    [Fact]
    public async Task AllAsync_EvaluatesContainsRule()
    {
        var flagData = new FlagData(
            "1",
            FlagType.String,
            "email-check",
            "Email Check",
            TestData.StringTarget("default"),
            new[] { TestData.Rule("email", "contains", "@example.com", "matched") });

        var manager = CreateManager(flagData).WithContext(new Context("user", identifier: "user-1", attributes: new[]
        {
            new ContextAttribute("email", new[] { "alice@example.com" })
        }));

        var flags = await manager.AllAsync();

        Assert.Single(flags);
        Assert.Equal("matched", flags[0].AsString());
    }

    [Fact]
    public async Task AllAsync_EvaluatesNotContainsRule()
    {
        var flagData = new FlagData(
            "1",
            FlagType.String,
            "email-check",
            "Email Check",
            TestData.StringTarget("default"),
            new[] { TestData.Rule("email", "notcontains", "@other.com", "matched") });

        var manager = CreateManager(flagData).WithContext(new Context("user", identifier: "user-1", attributes: new[]
        {
            new ContextAttribute("email", new[] { "alice@example.com" })
        }));

        var flags = await manager.AllAsync();

        Assert.Single(flags);
        Assert.Equal("matched", flags[0].AsString());
    }

    [Fact]
    public async Task SingleAsync_UsesRolloutTarget_WhenContextIsInBucket()
    {
        var flagData = new FlagData(
            "1",
            FlagType.Boolean,
            "rollout-flag",
            "Rollout Flag",
            TestData.BooleanTarget(false),
            Array.Empty<Rule>(),
            new RolloutData(TestData.BooleanTarget(true), Array.Empty<Rule>(), 50, "test-salt", "active"));

        var manager = CreateManager(flagData).WithContext(Context.Single("user", "user-0"));

        var flag = await manager.SingleAsync("rollout-flag");

        Assert.True(flag.IsEnabled());
    }

    [Fact]
    public async Task SingleAsync_UsesFallbackTarget_WhenContextIsOutsideBucket()
    {
        var flagData = new FlagData(
            "1",
            FlagType.Boolean,
            "rollout-flag",
            "Rollout Flag",
            TestData.BooleanTarget(false),
            Array.Empty<Rule>(),
            new RolloutData(TestData.BooleanTarget(true), Array.Empty<Rule>(), 50, "test-salt", "active"));

        var manager = CreateManager(flagData).WithContext(Context.Single("user", "user-2"));

        var flag = await manager.SingleAsync("rollout-flag");

        Assert.False(flag.IsEnabled());
    }

    [Fact]
    public async Task SingleAsync_ReportsUsageWithoutAnonymousContext()
    {
        var flagData = new FlagData("1", FlagType.Boolean, "simple", "Simple", TestData.BooleanTarget(true), Array.Empty<Rule>());
        var apiClient = new Helpers.FakeApiClient(new RulesResponse("1", new[] { flagData }));
        var manager = new FlagManager(apiClient, new NullCache(), new RuleEngine(), 60, NullLogger.Instance);

        _ = await manager.SingleAsync("simple");

        var usage = Assert.Single(apiClient.UsageReports);
        Assert.Equal("simple", usage.Key);
        Assert.Null(usage.Identifier);
        Assert.Null(usage.DefaultValue);
    }

    [Fact]
    public async Task SingleAsync_ReportsUsageWithInlineDefaultValue_WhenFlagMissing()
    {
        var apiClient = new Helpers.FakeApiClient(new RulesResponse("1", Array.Empty<FlagData>()));
        var manager = new FlagManager(apiClient, new NullCache(), new RuleEngine(), 60, NullLogger.Instance);

        _ = await manager.SingleAsync("missing-flag", true);

        var usage = Assert.Single(apiClient.UsageReports);
        Assert.Equal("missing-flag", usage.Key);
        Assert.Equal(true, usage.DefaultValue);
    }

    [Fact]
    public async Task SingleAsync_ReportsUsageWithDefaultsCollectionValue_WhenFlagMissing()
    {
        var apiClient = new Helpers.FakeApiClient(new RulesResponse("1", Array.Empty<FlagData>()));
        var defaults = new DefaultsCollection().Add("missing-flag", "fallback");
        var manager = new FlagManager(apiClient, new NullCache(), new RuleEngine(), 60, NullLogger.Instance).WithDefaults(defaults);

        _ = await manager.SingleAsync("missing-flag");

        var usage = Assert.Single(apiClient.UsageReports);
        Assert.Equal("missing-flag", usage.Key);
        Assert.Equal("fallback", usage.DefaultValue);
    }

    [Fact]
    public async Task SingleAsync_ReportsUsageWithInlineDefaultValue_WhenFlagFound()
    {
        var flagData = new FlagData("1", FlagType.Boolean, "simple", "Simple", TestData.BooleanTarget(true), Array.Empty<Rule>());
        var apiClient = new Helpers.FakeApiClient(new RulesResponse("1", new[] { flagData }));
        var manager = new FlagManager(apiClient, new NullCache(), new RuleEngine(), 60, NullLogger.Instance);

        _ = await manager.SingleAsync("simple", false);

        var usage = Assert.Single(apiClient.UsageReports);
        Assert.Equal("simple", usage.Key);
        Assert.Equal(false, usage.DefaultValue);
    }

    [Fact]
    public async Task SingleAsync_ReportsUsageWithDefaultsCollectionValue_WhenFlagFound()
    {
        var flagData = new FlagData("1", FlagType.String, "simple", "Simple", TestData.StringTarget("actual"), Array.Empty<Rule>());
        var apiClient = new Helpers.FakeApiClient(new RulesResponse("1", new[] { flagData }));
        var defaults = new DefaultsCollection().Add("simple", "fallback");
        var manager = new FlagManager(apiClient, new NullCache(), new RuleEngine(), 60, NullLogger.Instance).WithDefaults(defaults);

        _ = await manager.SingleAsync("simple");

        var usage = Assert.Single(apiClient.UsageReports);
        Assert.Equal("simple", usage.Key);
        Assert.Equal("fallback", usage.DefaultValue);
    }

    private static FlagManager CreateManager(params FlagData[] flags)
    {
        var apiClient = new Helpers.FakeApiClient(new RulesResponse("2026-02-24", flags));
        return new FlagManager(apiClient, new NullCache(), new RuleEngine(), 60, NullLogger.Instance);
    }
}