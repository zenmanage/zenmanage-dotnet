using System.Text.Json;

namespace Zenmanage;

/// <summary>
/// Supported flag value types.
/// </summary>
public enum FlagType
{
    Boolean,
    String,
    Number,
    Json,

    /// <summary>
    /// A flag type this SDK version does not recognize (for example, a type introduced
    /// on the wire after this SDK shipped). Evaluation treats flags of this type like a
    /// missing flag, falling back to the caller's default rather than throwing.
    /// </summary>
    Unknown
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
    double? Number = null,
    JsonElement? Json = null);

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