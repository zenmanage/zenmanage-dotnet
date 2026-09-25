using System.Text.Json;

namespace Zenmanage.Flagging;

/// <summary>
/// Represents a fully materialized feature flag.
/// </summary>
public sealed class Flag
{
    private static readonly JsonElement EmptyJsonArray = CreateEmptyJsonArray();

    public Flag(
        string version,
        FlagType type,
        string key,
        string name,
        FlagTarget target,
        IReadOnlyList<Rule>? rules = null,
        RolloutData? rollout = null)
    {
        Version = version;
        Type = type;
        Key = key;
        Name = name;
        Target = target;
        Rules = rules ?? Array.Empty<Rule>();
        Rollout = rollout;
    }

    public string Version { get; }

    public FlagType Type { get; }

    public string Key { get; }

    public string Name { get; }

    public FlagTarget Target { get; }

    public IReadOnlyList<Rule> Rules { get; }

    public RolloutData? Rollout { get; }

    public bool IsEnabled()
        => Type == FlagType.Boolean && GetTypedValue().Boolean.GetValueOrDefault();

    public bool AsBool()
    {
        var value = GetTypedValue();

        if (value.Boolean is not null)
        {
            return value.Boolean.Value;
        }

        // Per the cross-SDK coercion contract, every non-boolean type returns true
        // from AsBool() unconditionally -- the value is wrapped in a non-empty
        // structure, and that wrapper is truthy, regardless of the underlying value
        // (including a number flag set to 0, or a string flag set to "").
        if (value.Number is not null || value.String is not null || value.Json is not null)
        {
            return true;
        }

        return false;
    }

    public string AsString()
    {
        var value = GetTypedValue();

        if (value.String is not null)
        {
            return value.String;
        }

        if (value.Boolean is not null)
        {
            return value.Boolean.Value ? "true" : "false";
        }

        if (value.Number is not null)
        {
            return value.Number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    public double AsNumber()
    {
        var value = GetTypedValue();

        if (value.Number is not null)
        {
            return value.Number.Value;
        }

        if (value.String is not null && double.TryParse(value.String, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (value.Boolean is not null)
        {
            return value.Boolean.Value ? 1 : 0;
        }

        return 0;
    }

    /// <summary>
    /// Gets the flag value as decoded JSON. Both JSON objects and JSON arrays are
    /// returned as-is via <see cref="JsonElement"/>. For a non-json flag, or for a json
    /// flag whose decoded value isn't itself an object or array (a bare JSON scalar,
    /// which the API allows but real flags won't normally use), this returns an empty
    /// JSON array — the same safe-zero-value fallback <see cref="AsString"/>/<see cref="AsNumber"/>
    /// use for their types.
    /// </summary>
    public JsonElement AsJson()
    {
        var value = GetTypedValue();

        if (value.Json is { ValueKind: JsonValueKind.Object or JsonValueKind.Array } json)
        {
            return json;
        }

        return EmptyJsonArray;
    }

    private static JsonElement CreateEmptyJsonArray()
    {
        using var document = JsonDocument.Parse("[]");
        return document.RootElement.Clone();
    }

    public object GetValue()
    {
        var value = GetTypedValue();
        if (value.Boolean is not null)
        {
            return value.Boolean.Value;
        }

        if (value.String is not null)
        {
            return value.String;
        }

        if (value.Number is not null)
        {
            return value.Number.Value;
        }

        if (value.Json is not null)
        {
            return value.Json.Value;
        }

        return string.Empty;
    }

    public FlagData ToData() => new(Version, Type, Key, Name, Target, Rules, Rollout);

    public static Flag FromData(FlagData data)
        => new(data.Version, data.Type, data.Key, data.Name, data.Target, data.Rules, data.Rollout);

    private TypedValue GetTypedValue() => Target.Value.Value;
}