using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Zenmanage.Api;
using Zenmanage.Contexting;
using Zenmanage.Tests.Helpers;

namespace Zenmanage.Tests;

public sealed class ApiClientTests
{
    [Fact]
    public async Task GetRulesAsync_FetchesMetadataThenRules()
    {
        var handler = new TestHttpMessageHandler(request => request.RequestUri!.ToString() switch
        {
            "https://api.zenmanage.com/v1/flag-json" => TestHttpMessageHandler.Json("""
                {"data":{"cdn":"https://cdn.zenmanage.com","path":"/flags.json"}}
                """),
            "https://cdn.zenmanage.com/flags.json" => TestHttpMessageHandler.Json("""
                {"version":"2026-02-24","flags":[]}
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        var response = await client.GetRulesAsync();

        Assert.Equal("2026-02-24", response.Version);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("srv_test", handler.Requests[0].Headers.GetValues("X-API-Key").Single());
    }

    [Fact]
    public async Task ReportUsageAsync_SendsContextHeader_WhenContextHasIdentity()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));
        var context = Context.Single("user", "user-123", "Jane");

        await client.ReportUsageAsync("new-dashboard", context);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.zenmanage.com/v1/flags/new-dashboard/usage", request.RequestUri!.ToString());
        Assert.True(request.Headers.Contains("X-ZENMANAGE-CONTEXT"));
    }

    [Fact]
    public async Task ReportUsageAsync_DoesNothing_WhenDisabled()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, false, new HttpClient(handler));

        await client.ReportUsageAsync("new-dashboard", Context.Single("user", "user-123"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReportUsageAsync_SendsDefaultValueHeader()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await client.ReportUsageAsync("new-ui", null, true);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("{\"new-ui\":true}", request.Headers.GetValues("X-Default-Value").Single());
    }

    [Fact]
    public async Task ReportUsageAsync_SendsNonBooleanDefaultValueHeader()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await client.ReportUsageAsync("num-flag", null, 42);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("{\"num-flag\":42}", request.Headers.GetValues("X-Default-Value").Single());
    }

    [Fact]
    public async Task ReportUsageAsync_OmitsDefaultValueHeader_WhenNotProvided()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await client.ReportUsageAsync("new-ui", null);

        var request = Assert.Single(handler.Requests);
        Assert.False(request.Headers.Contains("X-Default-Value"));
    }

    [Fact]
    public async Task ReportUsageAsync_SendsBothContextAndDefaultValueHeaders()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));
        var context = Context.Single("user", "user-123");

        await client.ReportUsageAsync("num-flag", context, 42);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("{\"num-flag\":42}", request.Headers.GetValues("X-Default-Value").Single());
        Assert.True(request.Headers.Contains("X-ZENMANAGE-CONTEXT"));
    }

    [Fact]
    public async Task ReportUsageAsync_DoesNotMangleFlagKeyCasing_InDefaultValueHeader()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await client.ReportUsageAsync("myCamelCaseFlag", null, true);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("{\"myCamelCaseFlag\":true}", request.Headers.GetValues("X-Default-Value").Single());
    }
}