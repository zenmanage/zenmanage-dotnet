using Microsoft.Extensions.Logging.Abstractions;
using Zenmanage.Contexting;
using Zenmanage.Api;

namespace Zenmanage.Tests.Helpers;

internal sealed class FakeApiClient : ApiClient
{
    private readonly RulesResponse rulesResponse;

    public FakeApiClient(RulesResponse rulesResponse)
        : base("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(new TestHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be used in FakeApiClient"))))
    {
        this.rulesResponse = rulesResponse;
    }

    public List<(string Key, string? Identifier, object? DefaultValue)> UsageReports { get; } = new();

    public override Task<RulesResponse> GetRulesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(rulesResponse);

    public override Task ReportUsageAsync(string key, Context? context, object? defaultValue = null, CancellationToken cancellationToken = default)
    {
        UsageReports.Add((key, context?.Identifier, defaultValue));
        return Task.CompletedTask;
    }
}