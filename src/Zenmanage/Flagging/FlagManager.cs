using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zenmanage.Api;
using Zenmanage.Caching;
using Zenmanage.Contexting;
using Zenmanage.Defaults;
using Zenmanage.Exceptions;
using Zenmanage.Internal;
using Zenmanage.Rules;

namespace Zenmanage.Flagging;

/// <summary>
/// Loads, caches, and evaluates flags against the current context.
/// </summary>
public sealed class FlagManager : IFlagManager
{
    private const string CacheKey = "zenmanage_rules";

    private readonly ApiClient apiClient;
    private readonly IZenmanageCache cache;
    private readonly RuleEngine ruleEngine;
    private readonly int cacheTtl;
    private readonly ILogger logger;
    private readonly Context context;
    private readonly DefaultsCollection defaults;
    private IReadOnlyList<Flag>? flags;

    public FlagManager(ApiClient apiClient, IZenmanageCache cache, RuleEngine ruleEngine, int cacheTtl, ILogger logger)
        : this(apiClient, cache, ruleEngine, cacheTtl, logger, new Context("anonymous"), new DefaultsCollection(), null)
    {
    }

    private FlagManager(
        ApiClient apiClient,
        IZenmanageCache cache,
        RuleEngine ruleEngine,
        int cacheTtl,
        ILogger logger,
        Context context,
        DefaultsCollection defaults,
        IReadOnlyList<Flag>? flags)
    {
        this.apiClient = apiClient;
        this.cache = cache;
        this.ruleEngine = ruleEngine;
        this.cacheTtl = cacheTtl;
        this.logger = logger;
        this.context = context;
        this.defaults = defaults;
        this.flags = flags;
    }

    public async Task<IReadOnlyList<Flag>> AllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRulesLoadedAsync(cancellationToken).ConfigureAwait(false);
        return flags?.Select(EvaluateFlag).ToArray() ?? Array.Empty<Flag>();
    }

    public async Task<Flag> SingleAsync(string key, object? defaultValue = null, CancellationToken cancellationToken = default)
    {
        await EnsureRulesLoadedAsync(cancellationToken).ConfigureAwait(false);

        foreach (var flag in flags ?? Array.Empty<Flag>())
        {
            if (flag.Key == key)
            {
                await apiClient.ReportUsageAsync(key, GetUsageContext(), ResolveEffectiveDefault(key, defaultValue), cancellationToken).ConfigureAwait(false);
                return EvaluateFlag(flag);
            }
        }

        if (defaultValue is not null)
        {
            await apiClient.ReportUsageAsync(key, GetUsageContext(), defaultValue, cancellationToken).ConfigureAwait(false);
            return CreateFlagFromDefault(key, defaultValue);
        }

        if (defaults.Has(key))
        {
            var fallbackDefault = defaults.Get(key)!;
            await apiClient.ReportUsageAsync(key, GetUsageContext(), fallbackDefault, cancellationToken).ConfigureAwait(false);
            return CreateFlagFromDefault(key, fallbackDefault);
        }

        throw new EvaluationException($"Flag not found: {key}");
    }

    public IFlagManager WithContext(Context context)
        => new FlagManager(apiClient, cache, ruleEngine, cacheTtl, logger, context, defaults, flags);

    public IFlagManager WithDefaults(DefaultsCollection defaults)
        => new FlagManager(apiClient, cache, ruleEngine, cacheTtl, logger, context, defaults, flags);

    public async Task RefreshRulesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Refreshing rules from API");
        await LoadRulesFromApiAsync(cancellationToken).ConfigureAwait(false);
    }

    internal Context CurrentContext => context;

    private Context? GetUsageContext()
        => context.Type == "anonymous" && context.Name is null && context.Identifier is null && context.GetAttributes().Count == 0
            ? null
            : context;

    private object? ResolveEffectiveDefault(string key, object? defaultValue)
        => defaultValue ?? (defaults.Has(key) ? defaults.Get(key) : null);

    private async Task EnsureRulesLoadedAsync(CancellationToken cancellationToken)
    {
        if (flags is not null)
        {
            return;
        }

        var cached = await cache.GetAsync(CacheKey, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var response = JsonSerializer.Deserialize<RulesResponse>(cached, Serialization.JsonOptions);
                if (response?.Flags is not null)
                {
                    flags = response.Flags.Select(Flag.FromData).ToArray();
                    return;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to parse cached rules");
            }
        }

        await LoadRulesFromApiAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task LoadRulesFromApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.GetRulesAsync(cancellationToken).ConfigureAwait(false);
            flags = response.Flags.Select(Flag.FromData).ToArray();
            await cache.SetAsync(CacheKey, JsonSerializer.Serialize(response, Serialization.JsonOptions), cacheTtl, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            flags = Array.Empty<Flag>();
            throw;
        }
    }

    private Flag EvaluateFlag(Flag flag)
    {
        var rollout = flag.Rollout;
        var target = flag.Target;
        var rules = flag.Rules;

        if (rollout is not null)
        {
            var inBucket = RolloutEvaluator.IsInBucket(rollout.Salt, context.Identifier, rollout.Percentage);
            if (inBucket)
            {
                target = rollout.Target;
                rules = rollout.Rules;
            }
        }

        if (rules.Count == 0)
        {
            return ReferenceEquals(target, flag.Target) && ReferenceEquals(rules, flag.Rules)
                ? flag
                : new Flag(flag.Version, flag.Type, flag.Key, flag.Name, target, rules);
        }

        var matchedRule = ruleEngine.Evaluate(rules, context);
        if (matchedRule is not null)
        {
            var newTarget = target with { Value = matchedRule.Value };
            return new Flag(flag.Version, flag.Type, flag.Key, flag.Name, newTarget, rules);
        }

        return ReferenceEquals(target, flag.Target) && ReferenceEquals(rules, flag.Rules)
            ? flag
            : new Flag(flag.Version, flag.Type, flag.Key, flag.Name, target, rules);
    }

    private static Flag CreateFlagFromDefault(string key, object defaultValue)
    {
        return defaultValue switch
        {
            bool boolValue => new Flag("1", FlagType.Boolean, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Boolean: boolValue)))),
            byte byteValue => new Flag("1", FlagType.Number, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Number: byteValue)))),
            short shortValue => new Flag("1", FlagType.Number, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Number: shortValue)))),
            int intValue => new Flag("1", FlagType.Number, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Number: intValue)))),
            long longValue => new Flag("1", FlagType.Number, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Number: longValue)))),
            float floatValue => new Flag("1", FlagType.Number, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Number: floatValue)))),
            double doubleValue => new Flag("1", FlagType.Number, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Number: doubleValue)))),
            decimal decimalValue => new Flag("1", FlagType.Number, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Number: (double)decimalValue)))),
            _ => new Flag("1", FlagType.String, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(String: Convert.ToString(defaultValue, System.Globalization.CultureInfo.InvariantCulture)))))
        };
    }
}