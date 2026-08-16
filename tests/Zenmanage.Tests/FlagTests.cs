using Zenmanage.Flagging;

namespace Zenmanage.Tests;

public sealed class FlagTests
{
    [Fact]
    public void IsEnabled_ReturnsTrue_ForBooleanTrue()
    {
        var flag = new Flag("1", FlagType.Boolean, "test", "Test", TestData.BooleanTarget(true));

        Assert.True(flag.IsEnabled());
    }

    [Fact]
    public void AsString_ConvertsNonStringValues()
    {
        var flag = new Flag("1", FlagType.Number, "test", "Test", TestData.NumberTarget(42));

        Assert.Equal("42", flag.AsString());
    }

    [Fact]
    public void AsNumber_ParsesStringValues()
    {
        var flag = new Flag("1", FlagType.String, "test", "Test", TestData.StringTarget("3.14"));

        Assert.Equal(3.14, flag.AsNumber(), 3);
    }

    [Fact]
    public void GetValue_ReturnsUnderlyingValue()
    {
        var flag = new Flag("1", FlagType.String, "test", "Test", TestData.StringTarget("hello"));

        Assert.Equal("hello", flag.GetValue());
    }

    [Fact]
    public void FromData_CreatesEquivalentFlag()
    {
        var data = new FlagData("1", FlagType.Boolean, "test", "Test", TestData.BooleanTarget(true), Array.Empty<Rule>());

        var flag = Flag.FromData(data);

        Assert.Equal("test", flag.Key);
        Assert.True(flag.IsEnabled());
    }
}