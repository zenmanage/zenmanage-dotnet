namespace Zenmanage.Contexting;

/// <summary>
/// Represents a single attribute value.
/// </summary>
public sealed record Value(string Data)
{
    public Dictionary<string, string> ToDictionary() => new()
    {
        ["value"] = Data
    };
}