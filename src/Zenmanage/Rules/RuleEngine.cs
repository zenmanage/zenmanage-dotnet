using System.Text.Json;
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
            return clause.Operator is "isnull" or "notequal" or "notin" or "notcontains" or "notstartswith" or "notendswith";
        }

        var values = attribute.GetValues();
        var clauseValue = ToAttributeClauseValues(clause.Value);

        return clause.Operator switch
        {
            "equal" => EvaluateEquals(values, clauseValue),
            "notequal" => !EvaluateEquals(values, clauseValue),
            "contains" => EvaluateContains(values, clauseValue),
            "notcontains" => !EvaluateContains(values, clauseValue),
            "in" => EvaluateIn(values, clauseValue),
            "notin" => !EvaluateIn(values, clauseValue),
            "startswith" => EvaluateStartsWith(values, clauseValue),
            "notstartswith" => !EvaluateStartsWith(values, clauseValue),
            "endswith" => EvaluateEndsWith(values, clauseValue),
            "notendswith" => !EvaluateEndsWith(values, clauseValue),
            "gt" => EvaluateGreaterThan(values, clauseValue),
            "gte" => EvaluateGreaterThanOrEqual(values, clauseValue),
            "lt" => EvaluateLessThan(values, clauseValue),
            "lte" => EvaluateLessThanOrEqual(values, clauseValue),
            "isnull" => values.All(string.IsNullOrEmpty),
            "notnull" => values.Any(v => !string.IsNullOrEmpty(v)),
            _ => false
        };
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

        var values = new[] { context.Identifier };

        return clause.Operator switch
        {
            "equal" => EvaluateEquals(values, matchingTargets),
            "notequal" => !EvaluateEquals(values, matchingTargets),
            "contains" => EvaluateContains(values, matchingTargets),
            "notcontains" => !EvaluateContains(values, matchingTargets),
            "in" => EvaluateIn(values, matchingTargets),
            "notin" => !EvaluateIn(values, matchingTargets),
            "startswith" => EvaluateStartsWith(values, matchingTargets),
            "notstartswith" => !EvaluateStartsWith(values, matchingTargets),
            "endswith" => EvaluateEndsWith(values, matchingTargets),
            "notendswith" => !EvaluateEndsWith(values, matchingTargets),
            "gt" => EvaluateGreaterThan(values, matchingTargets),
            "gte" => EvaluateGreaterThanOrEqual(values, matchingTargets),
            "lt" => EvaluateLessThan(values, matchingTargets),
            "lte" => EvaluateLessThanOrEqual(values, matchingTargets),
            _ => false
        };
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

    private static bool EvaluateEquals(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues)
        => clauseValues.Count > 0 && values.Any(value => value == clauseValues[0]);

    private static bool EvaluateContains(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues)
        => clauseValues.Count > 0 && values.Any(value => value.Contains(clauseValues[0], StringComparison.Ordinal));

    private static bool EvaluateIn(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues)
        => clauseValues.Count > 0 && values.Any(value => clauseValues.Contains(value));

    private static bool EvaluateStartsWith(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues)
        => clauseValues.Count > 0 && values.Any(value => value.StartsWith(clauseValues[0], StringComparison.Ordinal));

    private static bool EvaluateEndsWith(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues)
        => clauseValues.Count > 0 && values.Any(value => value.EndsWith(clauseValues[0], StringComparison.Ordinal));

    private static bool EvaluateGreaterThan(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues)
        => CompareNumeric(values, clauseValues, (left, right) => left > right);

    private static bool EvaluateGreaterThanOrEqual(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues)
        => CompareNumeric(values, clauseValues, (left, right) => left >= right);

    private static bool EvaluateLessThan(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues)
        => CompareNumeric(values, clauseValues, (left, right) => left < right);

    private static bool EvaluateLessThanOrEqual(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues)
        => CompareNumeric(values, clauseValues, (left, right) => left <= right);

    private static bool CompareNumeric(IReadOnlyList<string> values, IReadOnlyList<string> clauseValues, Func<double, double, bool> comparison)
    {
        if (clauseValues.Count == 0 || !double.TryParse(clauseValues[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var right))
        {
            return false;
        }

        foreach (var value in values)
        {
            if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var left) && comparison(left, right))
            {
                return true;
            }
        }

        return false;
    }
}