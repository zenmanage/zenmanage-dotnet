using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zenmanage.Contexting;
using Zenmanage.Exceptions;
using Zenmanage.Internal;

namespace Zenmanage.Api;

/// <summary>
/// HTTP client used to fetch flag rules and report usage.
/// </summary>
public class ApiClient
{
    private const string RulesPath = "/v1/flag-json";
    private const int MaxRetries = 3;
    private const int RetryDelayMilliseconds = 100;

    // Deliberately not Serialization.JsonOptions: that config sets DictionaryKeyPolicy to
    // snake_case, which would mangle the flag key used as the dictionary key below.
    private static readonly JsonSerializerOptions DefaultValueHeaderOptions = new();

    private readonly HttpClient httpClient;
    private readonly ILogger logger;
    private readonly bool enableUsageReporting;
    public ApiClient(
        string environmentToken,
        string apiEndpoint,
        ILogger logger,
        bool enableUsageReporting = true,
        HttpClient? httpClient = null,
        string? clientAgent = null)
    {
        this.logger = logger;
        this.enableUsageReporting = enableUsageReporting;
        this.httpClient = httpClient ?? new HttpClient();
        this.httpClient.BaseAddress = new Uri(apiEndpoint, UriKind.Absolute);
        this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        this.httpClient.DefaultRequestHeaders.Add("X-API-Key", environmentToken);
        this.httpClient.DefaultRequestHeaders.Add("X-ZEN-CLIENT-AGENT", $"{clientAgent ?? SdkInfo.ClientAgent}/{SdkInfo.Version}");
    }

    /// <summary>
    /// Fetches metadata from the API and then loads the rules payload from the CDN URL.
    /// </summary>
    public virtual async Task<RulesResponse> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = RetryDelayMilliseconds * (int)Math.Pow(2, attempt - 1);
                    logger.LogDebug("Retrying rules fetch in {Delay}ms (attempt {Attempt}/{MaxRetries})", delay, attempt + 1, MaxRetries);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                var cdnUrl = await GetCdnRulesUrlAsync(cancellationToken).ConfigureAwait(false);
                using var request = new HttpRequestMessage(HttpMethod.Get, cdnUrl);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new FetchRulesException($"CDN request failed with status {(int)response.StatusCode}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var rules = await JsonSerializer.DeserializeAsync<RulesResponse>(stream, Serialization.JsonOptions, cancellationToken).ConfigureAwait(false);

                if (rules is null || string.IsNullOrWhiteSpace(rules.Version) || rules.Flags is null)
                {
                    throw new InvalidRulesException("Invalid response format from CDN");
                }

                return rules;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastError = exception;
                logger.LogWarning(exception, "Failed to fetch rules (attempt {Attempt}/{MaxRetries})", attempt + 1, MaxRetries);
            }
        }

        throw new FetchRulesException($"Failed to fetch rules after {MaxRetries} attempts: {lastError?.Message}", lastError!);
    }

    /// <summary>
    /// Reports flag usage. Errors are swallowed because usage reporting is non-critical.
    /// </summary>
    public virtual async Task ReportUsageAsync(string key, Context? context, object? defaultValue = null, CancellationToken cancellationToken = default)
    {
        if (!enableUsageReporting)
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/flags/{Uri.EscapeDataString(key)}/usage");

            if (context is not null && ShouldSendContext(context))
            {
                request.Headers.Add("X-ZENMANAGE-CONTEXT", JsonSerializer.Serialize(context.ToData(), Serialization.JsonOptions));
            }

            if (defaultValue is not null)
            {
                try
                {
                    var payload = new Dictionary<string, object?> { [key] = defaultValue };
                    request.Headers.Add("X-Default-Value", JsonSerializer.Serialize(payload, DefaultValueHeaderOptions));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogDebug(exception, "Failed to encode usage default value for {Key}", key);
                }
            }

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            _ = response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogDebug(exception, "Failed to report flag usage for {Key}", key);
        }
    }

    private async Task<string> GetCdnRulesUrlAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RulesPath);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new FetchRulesException($"API metadata request failed with status {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var metadata = await JsonSerializer.DeserializeAsync<MetadataEnvelope>(stream, Serialization.JsonOptions, cancellationToken).ConfigureAwait(false);

        if (metadata?.Data?.Cdn is null || metadata.Data.Path is null)
        {
            throw new InvalidRulesException("API response missing cdn or path fields");
        }

        return metadata.Data.Cdn + metadata.Data.Path;
    }

    private static bool ShouldSendContext(Context context)
        => !(context.Type == "anonymous"
            && context.Name is null
            && context.Identifier is null
            && context.GetAttributes().Count == 0);

    private sealed record MetadataEnvelope(MetadataData? Data);

    private sealed record MetadataData(string? Cdn, string? Path);
}