using Zenmanage.Contexting;

namespace Zenmanage.Tests;

public sealed class ContextTests
{
    [Fact]
    public void Value_ToDictionary_SerializesCorrectly()
    {
        var value = new Value("test-value");

        Assert.Equal("test-value", value.ToDictionary()["value"]);
    }

    [Fact]
    public void Attribute_AddValue_AppendsValues()
    {
        var attribute = new ContextAttribute("country", new[] { "US" });

        attribute.AddValue("CA");

        Assert.Equal(new[] { "US", "CA" }, attribute.GetValues());
    }

    [Fact]
    public void Context_Single_CreatesSimpleContext()
    {
        var context = Context.Single("user", "user-123", "Jane");

        Assert.Equal("user", context.Type);
        Assert.Equal("user-123", context.Identifier);
        Assert.Equal("Jane", context.Name);
    }

    [Fact]
    public void Context_FromData_RoundTripsAttributes()
    {
        var data = new ContextData(
            "user",
            "Jane",
            "user-123",
            new[] { new AttributeData("country", new[] { new ContextValueData("US"), new ContextValueData("CA") }) });

        var context = Context.FromData(data);

        Assert.True(context.HasAttribute("country"));
        Assert.Equal(new[] { "US", "CA" }, context.GetAttribute("country")!.GetValues());
        Assert.Equal("user-123", context.GetId());
    }

    [Fact]
    public void Context_ToData_OmitsEmptyAttributes()
    {
        var context = new Context("anonymous");

        var data = context.ToData();

        Assert.Equal("anonymous", data.Type);
        Assert.Null(data.Name);
        Assert.Null(data.Identifier);
        Assert.Null(data.Attributes);
    }
}