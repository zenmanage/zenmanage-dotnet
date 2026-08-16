namespace Zenmanage.Tests;

internal static class TestData
{
    public static FlagTarget BooleanTarget(bool value)
        => new(null, null, null, null, new FlagValueData(null, new TypedValue(Boolean: value)));

    public static FlagTarget StringTarget(string value)
        => new(null, null, null, null, new FlagValueData(null, new TypedValue(String: value)));

    public static FlagTarget NumberTarget(double value)
        => new(null, null, null, null, new FlagValueData(null, new TypedValue(Number: value)));

    public static Rule Rule(string attribute, string @operator, object value, bool result)
        => new(null, null, null, new[] { new RuleCondition(attribute, @operator, value) }, null, new FlagValueData(null, new TypedValue(Boolean: result)));

    public static Rule Rule(string attribute, string @operator, object value, string result)
        => new(null, null, null, new[] { new RuleCondition(attribute, @operator, value) }, null, new FlagValueData(null, new TypedValue(String: result)));
}