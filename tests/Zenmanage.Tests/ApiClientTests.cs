using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Zenmanage.Api;
using Zenmanage.Contexting;
using Zenmanage.Exceptions;
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
        Assert.Equal("srv_test", handler.Requests[0].Headers.GetValues("X-ZEN-API-KEY").Single());
    }

    [Fact]
    public async Task GetRulesAsync_RejectsNonHttpsCdnUrl()
    {
        var handler = new TestHttpMessageHandler(request => request.RequestUri!.ToString() switch
        {
            "https://api.zenmanage.com/v1/flag-json" => TestHttpMessageHandler.Json("""
                {"data":{"cdn":"http://cdn.zenmanage.com","path":"/flags.json"}}
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidRulesException>(() => client.GetRulesAsync());

        // Fails fast rather than retrying — only the single metadata request is made.
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetRulesAsync_DoesNotRetry_OnInvalidRulesResponse()
    {
        var handler = new TestHttpMessageHandler(request => request.RequestUri!.ToString() switch
        {
            "https://api.zenmanage.com/v1/flag-json" => TestHttpMessageHandler.Json("""
                {"data":{"cdn":"https://cdn.zenmanage.com","path":"/flags.json"}}
                """),
            "https://cdn.zenmanage.com/flags.json" => TestHttpMessageHandler.Json("{}"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidRulesException>(() => client.GetRulesAsync());

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ReportUsageAsync_RetriesOnFailure_ThenGivesUpWithoutThrowing()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await client.ReportUsageAsync("flaky-flag", null);

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task ReportUsageAsync_StopsRetrying_OnceSuccessful()
    {
        var attempt = 0;
        var handler = new TestHttpMessageHandler(_ =>
        {
            attempt++;
            return attempt < 2 ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await client.ReportUsageAsync("flaky-flag", null);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Dispose_DisposesOwnedHttpClient()
    {
        var apiClient = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance);

        apiClient.Dispose();

        var exception = await Assert.ThrowsAsync<FetchRulesException>(() => apiClient.GetRulesAsync());
        Assert.IsType<ObjectDisposedException>(exception.InnerException);
    }

    [Fact]
    public async Task Dispose_DoesNotDisposeExternallySuppliedHttpClient()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var externalClient = new HttpClient(handler);
        var apiClient = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, externalClient);

        apiClient.Dispose();

        // Disposing the ApiClient must not dispose a caller-supplied HttpClient — it's still usable.
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.zenmanage.com/ping");
        var exception = await Record.ExceptionAsync(() => externalClient.SendAsync(request));
        Assert.Null(exception);
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
        Assert.True(request.Headers.Contains("X-ZEN-CONTEXT"));
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
        Assert.Equal("{\"new-ui\":true}", request.Headers.GetValues("X-ZEN-DEFAULT-VALUE").Single());
    }

    [Fact]
    public async Task ReportUsageAsync_SendsNonBooleanDefaultValueHeader()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await client.ReportUsageAsync("num-flag", null, 42);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("{\"num-flag\":42}", request.Headers.GetValues("X-ZEN-DEFAULT-VALUE").Single());
    }

    [Fact]
    public async Task ReportUsageAsync_OmitsDefaultValueHeader_WhenNotProvided()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await client.ReportUsageAsync("new-ui", null);

        var request = Assert.Single(handler.Requests);
        Assert.False(request.Headers.Contains("X-ZEN-DEFAULT-VALUE"));
    }

    [Fact]
    public async Task ReportUsageAsync_SendsBothContextAndDefaultValueHeaders()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));
        var context = Context.Single("user", "user-123");

        await client.ReportUsageAsync("num-flag", context, 42);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("{\"num-flag\":42}", request.Headers.GetValues("X-ZEN-DEFAULT-VALUE").Single());
        Assert.True(request.Headers.Contains("X-ZEN-CONTEXT"));
    }

    [Fact]
    public async Task ReportUsageAsync_DoesNotMangleFlagKeyCasing_InDefaultValueHeader()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        var client = new ApiClient("srv_test", "https://api.zenmanage.com", NullLogger.Instance, true, new HttpClient(handler));

        await client.ReportUsageAsync("myCamelCaseFlag", null, true);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("{\"myCamelCaseFlag\":true}", request.Headers.GetValues("X-ZEN-DEFAULT-VALUE").Single());
    }
}