namespace Zenmanage.Contexting;

/// <summary>
/// Represents a context attribute with one or more values.
/// </summary>
public sealed class Attribute
{
    private readonly List<Value> values = new();

    public Attribute(string key, IEnumerable<string>? values = null)
    {
        Key = key;

        if (values is not null)
        {
            foreach (var value in values)
            {
                AddValue(value);
            }
        }
    }

    public string Key { get; }

    public IReadOnlyList<string> GetValues() => values.Select(value => value.Data).ToArray();

    public Attribute AddValue(string value)
    {
        values.Add(new Value(value));
        return this;
    }

    public AttributeData ToData() => new(Key, values.Select(value => new ContextValueData(value.Data)).ToArray());
}