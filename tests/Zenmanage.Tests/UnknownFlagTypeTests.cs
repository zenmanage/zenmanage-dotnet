using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Zenmanage.Api;
using Zenmanage.Caching;
using Zenmanage.Exceptions;
using Zenmanage.Flagging;
using Zenmanage.Rules;
using Zenmanage.Tests.Helpers;

namespace Zenmanage.Tests;

/// <summary>
/// The API is about to start serving a fourth flag type ("json") that this SDK's
/// <see cref="FlagType"/> enum does not yet know about. A rules payload containing a
/// json-typed flag must not break evaluation of the other, recognized flags, and
/// looking up the json-typed flag itself must degrade like a missing flag (fall back
/// to the caller's default) instead of throwing.
/// </summary>
public sealed class UnknownFlagTypeTests
{
    private const string RulesPayload = """
        {
          "version": "2026-02-24",
          "flags": [
            {
              "version": "1",
              "type": "boolean",
              "key": "bool-flag",
              "name": "Bool Flag",
              "target": { "value": { "value": { "boolean": true } } }
            },
            {
              "version": "1",
              "type": "string",
              "key": "string-flag",
              "name": "String Flag",
              "target": { "value": { "value": { "string": "hello" } } }
            },
            {
              "version": "1",
              "type": "number",
              "key": "number-flag",
              "name": "Number Flag",
              "target": { "value": { "value": { "number": 42 } } }
            },
            {
              "version": "1",
              "type": "json",
              "key": "json-flag",
              "name": "Json Flag",
              "target": { "value": { "value": { "json": { "nested": { "enabled": true }, "list": [1, 2, 3] } } } }
            }
          ]
        }
        """;

    [Fact]
    public async Task RulesPayloadWithUnknownFlagType_DoesNotPreventOtherFlagsFromEvaluating()
    {
        var manager = CreateManagerWithLiveJsonPayload();

        var boolFlag = await manager.SingleAsync("bool-flag");
        var stringFlag = await manager.SingleAsync("string-flag");
        var numberFlag = await manager.SingleAsync("number-flag");

        Assert.True(boolFlag.IsEnabled());
        Assert.Equal("hello", stringFlag.AsString());
        Assert.Equal(42, numberFlag.AsNumber());
    }

    [Fact]
    public async Task RulesPayloadWithUnknownFlagType_LookingUpItReturnsCallerDefault_InsteadOfThrowing()
    {
        var manager = CreateManagerWithLiveJsonPayload();

        var flag = await manager.SingleAsync("json-flag", "fallback-value");

        Assert.Equal("fallback-value", flag.AsString());
    }

    [Fact]
    public async Task RulesPayloadWithUnknownFlagType_LookingUpItThrows_WhenNoDefaultExists()
    {
        var manager = CreateManagerWithLiveJsonPayload();

        await Assert.ThrowsAsync<EvaluationException>(() => manager.SingleAsync("json-flag"));
    }

    [Fact]
    public async Task AllAsync_WithUnknownFlagType_OmitsIt_ButReturnsRecognizedFlags()
    {
        var manager = CreateManagerWithLiveJsonPayload();

        var flags = await manager.AllAsync();

        Assert.Contains(flags, f => f.Key == "bool-flag");
        Assert.Contains(flags, f => f.Key == "string-flag");
        Assert.Contains(flags, f => f.Key == "number-flag");
        Assert.DoesNotContain(flags, f => f.Key == "json-flag");
    }

    private static FlagManager CreateManagerWithLiveJsonPayload()
    {
        var handler = new TestHttpMessageHandler(request => request.RequestUri!.ToString() switch
        {
            "https://api.zenmanage.com/v1/flag-json" => TestHttpMessageHandler.Json("""
                {"data":{"cdn":"https://cdn.zenmanage.com","path":"/flags.json"}}
                """),
            "https://cdn.zenmanage.com/flags.json" => TestHttpMessageHandler.Json(RulesPayload),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var apiClient = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, false, new HttpClient(handler));
        return new FlagManager(apiClient, new NullCache(), new RuleEngine(), 60, NullLogger.Instance);
    }
}
