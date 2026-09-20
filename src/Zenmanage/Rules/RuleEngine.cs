using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Zenmanage.Contexting;

namespace Zenmanage.Rules;

/// <summary>
/// Evaluates rules against a context. Rules are processed in order and the first match wins.
/// </summary>
public sealed class RuleEngine
{
    public Rule? Evaluate(IReadOnlyList<Rule> rules, Context context)
    {
        foreach (var rule in rules)
        {
            if (EvaluateRule(rule, context))
            {
                return rule;
            }
        }

        return null;
    }

    private bool EvaluateRule(Rule rule, Context context)
    {
        if (rule.Clauses is { Count: > 0 })
        {
            return rule.Clauses.All(clause => EvaluateClause(clause, context));
        }

        if (rule.Criteria is not null)
        {
            return EvaluateClause(rule.Criteria, context);
        }

        return true;
    }

    private bool EvaluateClause(RuleCondition clause, Context context)
    {
        if (clause.Attribute is "context" or "segment")
        {
            return EvaluateContextClause(clause, context);
        }

        var attribute = context.GetAttribute(clause.Attribute);
        if (attribute is null)
        {
            return clause.Operator is "isnull" or "notequal" or "notin" or "notcontains" or "notstartswith" or "notendswith" or "notregex";
        }

        var values = attribute.GetValues();

        if (clause.Operator is "isnull")
        {
            return values.All(string.IsNullOrEmpty);
        }

        if (clause.Operator is "notnull")
        {
            return values.Any(v => !string.IsNullOrEmpty(v));
        }

        var clauseValues = ToAttributeClauseValues(clause.Value);
        return EvaluateAttributeOperator(clause.Operator, values, clauseValues);
    }

    /// <summary>
    /// Evaluates a non-null/not-null attribute operator against every (attribute value, clause
    /// value) combination. Mirrors the PHP SDK's AttributeConditionEvaluator:
    /// - "in"/"not_in" treat <paramref name="clauseValues"/> as a single list and, for a given
    ///   attribute value, ask whether it is (or is not) a member of that list; the overall result
    ///   is true if ANY attribute value satisfies that per-value check.
    /// - Every other negated operator (not_equal, not_contains, ...) is true only if NO
    ///   (attribute value, clause value) pair satisfies the positive comparison — i.e. negation is
    ///   applied to the aggregate match, not to each pair independently.
    /// </summary>
    private static bool EvaluateAttributeOperator(string @operator, IReadOnlyList<string> attributeValues, IReadOnlyList<string> clauseValues)
    {
        var (baseOperator, negate) = SplitNegation(@operator);

        if (baseOperator is "in")
        {
            var isIn = attributeValues.Any(clauseValues.Contains);
            return negate ? attributeValues.Any(value => !clauseValues.Contains(value)) : isIn;
        }

        if (clauseValues.Count == 0)
        {
            return false;
        }

        var anyPositiveMatch = attributeValues.Any(value => clauseValues.Any(clauseValue => MatchesSingle(baseOperator, value, clauseValue)));
        return negate ? !anyPositiveMatch : anyPositiveMatch;
    }

    private bool EvaluateContextClause(RuleCondition clause, Context context)
    {
        if (context.Identifier is null)
        {
            return false;
        }

        var targets = ToContextTargets(clause.Value);
        if (targets.Count == 0)
        {
            return false;
        }

        var matchingTargets = targets
            .Where(target => target.Type is null || target.Type == context.Type)
            .Select(target => target.Identifier)
            .ToArray();

        if (matchingTargets.Length == 0)
        {
            return false;
        }

        return EvaluateContextOperator(clause.Operator, context.Identifier, matchingTargets);
    }

    /// <summary>
    /// Evaluates a context/segment operator by checking the context identifier against every
    /// matching target and returning true if ANY target satisfies the (possibly negated) operator.
    /// Mirrors the PHP SDK's ContextConditionEvaluator/SegmentConditionEvaluator, which evaluate
    /// each candidate target independently (negation included) and OR the results — unlike
    /// attribute clauses, there is no "all pairs must fail" aggregation here.
    /// </summary>
    private static bool EvaluateContextOperator(string @operator, string identifier, IReadOnlyList<string> targetIdentifiers)
    {
        if (targetIdentifiers.Count == 0)
        {
            return false;
        }

        var (baseOperator, negate) = SplitNegation(@operator);

        if (baseOperator is "in")
        {
            var isIn = targetIdentifiers.Contains(identifier);
            return negate ? !isIn : isIn;
        }

        return negate
            ? targetIdentifiers.Any(target => !MatchesSingle(baseOperator, identifier, target))
            : targetIdentifiers.Any(target => MatchesSingle(baseOperator, identifier, target));
    }

    private static IReadOnlyList<RuleContextTarget> ToContextTargets(object? value)
    {
        if (value is null)
        {
            return Array.Empty<RuleContextTarget>();
        }

        if (value is RuleContextTarget target)
        {
            return new[] { target };
        }

        if (value is IEnumerable<RuleContextTarget> targets)
        {
            return targets.ToArray();
        }

        if (value is string identifier)
        {
            return new[] { new RuleContextTarget(identifier) };
        }

        if (value is IEnumerable<string> strings)
        {
            return strings.Select(entry => new RuleContextTarget(entry)).ToArray();
        }

        if (value is JsonElement json)
        {
            return ParseContextTargets(json);
        }

        return Array.Empty<RuleContextTarget>();
    }

    private static IReadOnlyList<string> ToAttributeClauseValues(object? value)
    {
        if (value is null)
        {
            return Array.Empty<string>();
        }

        if (value is string entry)
        {
            return new[] { entry };
        }

        if (value is IEnumerable<string> strings)
        {
            return strings.ToArray();
        }

        if (value is JsonElement json)
        {
            return ParseStrings(json);
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<RuleContextTarget> ParseContextTargets(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.String)
        {
            return new[] { new RuleContextTarget(json.GetString()!) };
        }

        if (json.ValueKind == JsonValueKind.Object)
        {
            var identifier = json.GetProperty("identifier").GetString();
            string? type = null;
            if (json.TryGetProperty("type", out var typeProperty) && typeProperty.ValueKind != JsonValueKind.Null)
            {
                type = typeProperty.GetString();
            }

            return identifier is null ? Array.Empty<RuleContextTarget>() : new[] { new RuleContextTarget(identifier, type) };
        }

        if (json.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RuleContextTarget>();
        }

        var targets = new List<RuleContextTarget>();
        foreach (var item in json.EnumerateArray())
        {
            targets.AddRange(ParseContextTargets(item));
        }

        return targets;
    }

    private static IReadOnlyList<string> ParseStrings(JsonElement json)
    {
        return json.ValueKind switch
        {
            JsonValueKind.String => new[] { json.GetString() ?? string.Empty },
            JsonValueKind.Number => new[] { json.ToString() },
            JsonValueKind.True => new[] { "true" },
            JsonValueKind.False => new[] { "false" },
            JsonValueKind.Array => json.EnumerateArray().SelectMany(ParseStrings).ToArray(),
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Splits an operator token into its positive base form and whether it was negated
    /// (prefixed with "not"), e.g. "notcontains" → ("contains", true).
    /// </summary>
    private static (string BaseOperator, bool Negate) SplitNegation(string @operator)
        => @operator.StartsWith("not", StringComparison.Ordinal) && @operator.Length > 3
            ? (@operator["not".Length..], true)
            : (@operator, false);

    /// <summary>Evaluates one positive (non-negated, non-list) operator against a single value pair.</summary>
    private static bool MatchesSingle(string baseOperator, string actual, string expected) => baseOperator switch
    {
        "equal" => actual == expected,
        "contains" => actual.Contains(expected, StringComparison.Ordinal),
        "startswith" => actual.StartsWith(expected, StringComparison.Ordinal),
        "endswith" => actual.EndsWith(expected, StringComparison.Ordinal),
        "gt" => CompareNumeric(actual, expected, (left, right) => left > right),
        "gte" => CompareNumeric(actual, expected, (left, right) => left >= right),
        "lt" => CompareNumeric(actual, expected, (left, right) => left < right),
        "lte" => CompareNumeric(actual, expected, (left, right) => left <= right),
        "regex" => MatchesRegex(actual, expected),
        _ => false
    };

    private static bool CompareNumeric(string actual, string expected, Func<double, double, bool> comparison)
        => double.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out var left)
            && double.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var right)
            && comparison(left, right);

    /// <summary>
    /// Matches a value against a regex pattern, accepting either a raw .NET pattern or a
    /// PCRE-style delimited pattern (e.g. "/^foo$/i") as produced by the reference PHP SDK's
    /// preg_match-based implementation. Invalid patterns fail closed (return false), matching PHP.
    /// </summary>
    private static bool MatchesRegex(string actual, string pattern)
    {
        try
        {
            var (body, options) = ParsePattern(pattern);
            return Regex.IsMatch(actual, body, options);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static (string Pattern, RegexOptions Options) ParsePattern(string pattern)
    {
        if (pattern.Length < 2 || char.IsLetterOrDigit(pattern[0]) || pattern[0] == '\\')
        {
            return (pattern, RegexOptions.None);
        }

        var delimiter = pattern[0];
        var closingIndex = pattern.LastIndexOf(delimiter);
        if (closingIndex <= 0)
        {
            return (pattern, RegexOptions.None);
        }

        var body = pattern[1..closingIndex];
        var options = RegexOptions.None;
        foreach (var flag in pattern[(closingIndex + 1)..])
        {
            options |= flag switch
            {
                'i' => RegexOptions.IgnoreCase,
                'm' => RegexOptions.Multiline,
                's' => RegexOptions.Singleline,
                'x' => RegexOptions.IgnorePatternWhitespace,
                _ => RegexOptions.None
            };
        }

        return (body, options);
    }
}