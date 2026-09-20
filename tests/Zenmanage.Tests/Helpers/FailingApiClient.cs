using Microsoft.Extensions.Logging.Abstractions;
using Zenmanage.Api;
using Zenmanage.Contexting;
using Zenmanage.Exceptions;

namespace Zenmanage.Tests.Helpers;

/// <summary>An <see cref="ApiClient"/> whose rule loading always fails, for exercising fallback behavior.</summary>
internal sealed class FailingApiClient : ApiClient
{
    public FailingApiClient()
        : base("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(new TestHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be used in FailingApiClient"))))
    {
    }

    public List<(string Key, string? Identifier, object? DefaultValue)> UsageReports { get; } = new();

    public override Task<RulesResponse> GetRulesAsync(CancellationToken cancellationToken = default)
        => throw new FetchRulesException("Simulated API failure");

    public override Task ReportUsageAsync(string key, Context? context, object? defaultValue = null, CancellationToken cancellationToken = default)
    {
        UsageReports.Add((key, context?.Identifier, defaultValue));
        return Task.CompletedTask;
    }
}
