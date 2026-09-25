using System.Text.Json;
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

    [Fact]
    public void AsJson_ReturnsDecodedObject_ForJsonFlag()
    {
        var flag = new Flag("1", FlagType.Json, "test", "Test", TestData.JsonTarget("""{"nested":{"enabled":true},"list":[1,2,3]}"""));

        var json = flag.AsJson();

        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        Assert.True(json.GetProperty("nested").GetProperty("enabled").GetBoolean());
        Assert.Equal(3, json.GetProperty("list").GetArrayLength());
    }

    [Fact]
    public void AsJson_ReturnsDecodedArray_ForJsonFlag()
    {
        var flag = new Flag("1", FlagType.Json, "test", "Test", TestData.JsonTarget("[1,2,3]"));

        var json = flag.AsJson();

        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.Equal(3, json.GetArrayLength());
    }

    [Fact]
    public void AsJson_ReturnsEmptyArray_ForJsonFlagWithScalarValue()
    {
        var flag = new Flag("1", FlagType.Json, "test", "Test", TestData.JsonTarget("5"));

        var json = flag.AsJson();

        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.Equal(0, json.GetArrayLength());
    }

    [Fact]
    public void AsJson_ReturnsEmptyArray_ForNonJsonFlag()
    {
        var flag = new Flag("1", FlagType.String, "test", "Test", TestData.StringTarget("hello"));

        var json = flag.AsJson();

        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.Equal(0, json.GetArrayLength());
    }

    [Fact]
    public void AsString_ReturnsEmptyString_ForJsonFlag()
    {
        var flag = new Flag("1", FlagType.Json, "test", "Test", TestData.JsonTarget("[1,2,3]"));

        Assert.Equal(string.Empty, flag.AsString());
    }

    [Fact]
    public void AsNumber_ReturnsZero_ForJsonFlag()
    {
        var flag = new Flag("1", FlagType.Json, "test", "Test", TestData.JsonTarget("[1,2,3]"));

        Assert.Equal(0, flag.AsNumber());
    }

    [Fact]
    public void AsBool_ReturnsFalse_ForJsonFlag()
    {
        var flag = new Flag("1", FlagType.Json, "test", "Test", TestData.JsonTarget("[1,2,3]"));

        Assert.False(flag.AsBool());
    }

    [Fact]
    public void GetValue_ReturnsDecodedJsonElement_ForJsonFlag()
    {
        var flag = new Flag("1", FlagType.Json, "test", "Test", TestData.JsonTarget("""{"a":1}"""));

        var value = Assert.IsType<JsonElement>(flag.GetValue());
        Assert.Equal(1, value.GetProperty("a").GetInt32());
    }
}