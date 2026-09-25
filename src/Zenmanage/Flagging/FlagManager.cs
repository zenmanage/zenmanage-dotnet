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
        var loadedFlags = await LoadFlagsOrFallBackToDefaultsAsync(cancellationToken).ConfigureAwait(false);

        var evaluatedByKey = new Dictionary<string, Flag>(StringComparer.Ordinal);
        foreach (var flag in loadedFlags)
        {
            // A flag of a type this SDK version does not recognize (e.g. one introduced
            // on the wire after this SDK shipped) is treated like a missing flag: it's
            // omitted here so a matching DefaultsCollection entry can fill it in below.
            if (flag.Type == FlagType.Unknown)
            {
                continue;
            }

            var evaluated = EvaluateFlag(flag);
            evaluatedByKey[evaluated.Key] = evaluated;
        }

        foreach (var (key, value) in defaults.All())
        {
            if (!evaluatedByKey.ContainsKey(key))
            {
                evaluatedByKey[key] = CreateFlagFromDefault(key, value);
            }
        }

        return evaluatedByKey.Values.ToArray();
    }

    public async Task<Flag> SingleAsync(string key, object? defaultValue = null, CancellationToken cancellationToken = default)
    {
        var loadedFlags = await LoadFlagsOrFallBackToDefaultsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var flag in loadedFlags)
        {
            // A flag of a type this SDK version does not recognize is treated like a
            // missing flag rather than evaluated, so it falls through to the caller's
            // default below instead of returning a bogus/empty value.
            if (flag.Key == key && flag.Type != FlagType.Unknown)
            {
                await apiClient.ReportUsageAsync(key, GetUsageContext(), ResolveEffectiveDefault(key, defaultValue), cancellationToken).ConfigureAwait(false);
                return EvaluateFlag(flag);
            }
        }

        // Flag not found (including when rule-loading failed outright, or the flag's
        // type is unrecognized by this SDK version): fall back to the effective
        // default (inline parameter, prioritized over a DefaultsCollection entry), if
        // one exists.
        var effectiveDefault = ResolveEffectiveDefault(key, defaultValue);
        if (effectiveDefault is not null)
        {
            await apiClient.ReportUsageAsync(key, GetUsageContext(), effectiveDefault, cancellationToken).ConfigureAwait(false);
            return CreateFlagFromDefault(key, effectiveDefault);
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

    /// <summary>
    /// Loads the current flag set, falling back to an empty list (so callers fall
    /// through to their own defaults handling) if rule-loading fails outright.
    /// </summary>
    private async Task<IReadOnlyList<Flag>> LoadFlagsOrFallBackToDefaultsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureRulesLoadedAsync(cancellationToken).ConfigureAwait(false);
            return flags ?? Array.Empty<Flag>();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Failed to load rules, falling back to configured defaults");
            return Array.Empty<Flag>();
        }
    }

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
                    LogUnknownFlagTypes(flags);
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
        var response = await apiClient.GetRulesAsync(cancellationToken).ConfigureAwait(false);
        flags = response.Flags?.Select(Flag.FromData).ToArray() ?? [];
        LogUnknownFlagTypes(flags);
        await cache.SetAsync(CacheKey, JsonSerializer.Serialize(response, Serialization.JsonOptions), cacheTtl, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Logs once per loaded flag set (rather than silently swallowing it) when the
    /// payload contains a flag whose type this SDK version does not recognize, e.g. a
    /// type introduced on the wire after this SDK shipped. Those flags are treated as
    /// missing during evaluation instead of throwing.
    /// </summary>
    private void LogUnknownFlagTypes(IReadOnlyList<Flag> loadedFlags)
    {
        foreach (var flag in loadedFlags)
        {
            if (flag.Type == FlagType.Unknown)
            {
                logger.LogWarning(
                    "Flag '{Key}' has a type this SDK version does not recognize; treating it as missing and falling back to the caller's default",
                    flag.Key);
            }
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
            JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Array } jsonValue
                => new Flag("1", FlagType.Json, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Json: jsonValue)))),
            System.Collections.IDictionary dictionaryValue
                => CreateJsonFlagFromDefault(key, dictionaryValue),
            System.Collections.IEnumerable enumerableValue and not string
                => CreateJsonFlagFromDefault(key, enumerableValue),
            _ => new Flag("1", FlagType.String, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(String: Convert.ToString(defaultValue, System.Globalization.CultureInfo.InvariantCulture)))))
        };
    }

    /// <summary>
    /// Object/array defaults (e.g. a <see cref="System.Collections.IDictionary"/> or a
    /// list) are typed as json rather than falling through to the string branch and
    /// being stringified, matching the PHP reference SDK's default-value typing.
    /// </summary>
    private static Flag CreateJsonFlagFromDefault(string key, object defaultValue)
    {
        var json = JsonSerializer.SerializeToElement(defaultValue, Serialization.JsonOptions);
        return new Flag("1", FlagType.Json, key, key, new FlagTarget(null, null, null, null, new FlagValueData(null, new TypedValue(Json: json))));
    }
}