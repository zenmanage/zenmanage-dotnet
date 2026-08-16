namespace Zenmanage.Flagging;

/// <summary>
/// Represents a fully materialized feature flag.
/// </summary>
public sealed class Flag
{
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

        if (value.Number is not null)
        {
            return Math.Abs(value.Number.Value) > double.Epsilon;
        }

        if (value.String is not null)
        {
            return !string.IsNullOrEmpty(value.String);
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

        return string.Empty;
    }

    public FlagData ToData() => new(Version, Type, Key, Name, Target, Rules, Rollout);

    public static Flag FromData(FlagData data)
        => new(data.Version, data.Type, data.Key, data.Name, data.Target, data.Rules, data.Rollout);

    private TypedValue GetTypedValue() => Target.Value.Value;
}