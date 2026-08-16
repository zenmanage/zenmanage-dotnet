namespace Zenmanage.Contexting;

/// <summary>
/// Represents the evaluation context used for flag targeting.
/// </summary>
public sealed class Context
{
    private readonly Dictionary<string, Attribute> attributes = new(StringComparer.Ordinal);

    public Context(string type, string? name = null, string? identifier = null, IEnumerable<Attribute>? attributes = null)
    {
        Type = type;
        Name = name;
        Identifier = identifier;

        if (attributes is not null)
        {
            foreach (var attribute in attributes)
            {
                AddAttribute(attribute);
            }
        }
    }

    public string Type { get; }

    public string? Name { get; }

    public string? Identifier { get; }

    public string? GetId() => Identifier;

    public static Context Single(string type, string identifier, string? name = null) => new(type, name, identifier);

    public static Context FromData(ContextData data)
    {
        var context = new Context(data.Type, data.Name, data.Identifier);

        if (data.Attributes is not null)
        {
            foreach (var attribute in data.Attributes)
            {
                context.AddAttribute(new Attribute(attribute.Key, attribute.Values.Select(value => value.Value)));
            }
        }

        return context;
    }

    public Context AddAttribute(Attribute attribute)
    {
        attributes[attribute.Key] = attribute;
        return this;
    }

    public Attribute? GetAttribute(string key) => attributes.GetValueOrDefault(key);

    public bool HasAttribute(string key) => attributes.ContainsKey(key);

    public IReadOnlyList<Attribute> GetAttributes() => attributes.Values.ToArray();

    public ContextData ToData() => new(Type, Name, Identifier, attributes.Count == 0 ? null : attributes.Values.Select(attribute => attribute.ToData()).ToArray());
}