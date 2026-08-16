namespace Zenmanage;

/// <summary>
/// Supported flag value types.
/// </summary>
public enum FlagType
{
    Boolean,
    String,
    Number
}

public sealed record ContextValueData(string Value);

public sealed record AttributeData(string Key, IReadOnlyList<ContextValueData> Values);

public sealed record ContextData(
    string Type,
    string? Name = null,
    string? Identifier = null,
    IReadOnlyList<AttributeData>? Attributes = null);

public sealed record RuleContextTarget(string Identifier, string? Type = null);

public sealed record TypedValue(
    bool? Boolean = null,
    string? String = null,
    double? Number = null);

public sealed record FlagValueData(string? Version, TypedValue Value);

public sealed record FlagTarget(
    string? Version,
    DateTimeOffset? ExpiredAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ScheduledAt,
    FlagValueData Value);

public sealed record RuleCondition(
    string Attribute,
    string Operator,
    object? Value = null);

public sealed record Rule(
    string? Version,
    string? Description,
    RuleCondition? Criteria,
    IReadOnlyList<RuleCondition>? Clauses,
    int? Position,
    FlagValueData Value);

public sealed record RolloutData(
    FlagTarget Target,
    IReadOnlyList<Rule> Rules,
    int Percentage,
    string Salt,
    string Status);

public sealed record FlagData(
    string Version,
    FlagType Type,
    string Key,
    string Name,
    FlagTarget Target,
    IReadOnlyList<Rule>? Rules = null,
    RolloutData? Rollout = null);

public sealed record RulesResponse(string Version, IReadOnlyList<FlagData> Flags);