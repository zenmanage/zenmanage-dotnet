namespace Zenmanage.Defaults;

/// <summary>
/// Reusable default values applied when a flag is missing.
/// </summary>
public sealed class DefaultsCollection
{
    private readonly Dictionary<string, object> values = new(StringComparer.Ordinal);

    public DefaultsCollection Add(string key, bool value)
    {
        values[key] = value;
        return this;
    }

    public DefaultsCollection Add(string key, string value)
    {
        values[key] = value;
        return this;
    }

    public DefaultsCollection Add(string key, double value)
    {
        values[key] = value;
        return this;
    }

    public bool Has(string key) => values.ContainsKey(key);

    public object? Get(string key) => values.GetValueOrDefault(key);

    public static DefaultsCollection FromDictionary(IReadOnlyDictionary<string, object> values)
    {
        var collection = new DefaultsCollection();
        foreach (var pair in values)
        {
            collection.values[pair.Key] = pair.Value;
        }

        return collection;
    }
}