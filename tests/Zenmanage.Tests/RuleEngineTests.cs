using Zenmanage.Contexting;
using Zenmanage.Rules;

namespace Zenmanage.Tests;

public sealed class RuleEngineTests
{
    private readonly RuleEngine engine = new();

    [Fact]
    public void Evaluate_ReturnsNull_WhenNoRuleMatches()
    {
        var rules = new[]
        {
            TestData.Rule("country", "equal", "US", true)
        };
        var context = new Context("user").AddAttribute(new ContextAttribute("country", new[] { "CA" }));

        Assert.Null(engine.Evaluate(rules, context));
    }

    [Fact]
    public void Evaluate_ReturnsFirstMatchingRule()
    {
        var ruleA = TestData.Rule("country", "equal", "US", true);
        var ruleB = TestData.Rule("country", "equal", "CA", false);
        var context = new Context("user").AddAttribute(new ContextAttribute("country", new[] { "US" }));

        var result = engine.Evaluate(new[] { ruleA, ruleB }, context);

        Assert.Equal(ruleA, result);
    }

    [Theory]
    [InlineData("equal", "premium", true)]
    [InlineData("notequal", "basic", true)]
    [InlineData("contains", "@acme.com", true)]
    [InlineData("startswith", "john", true)]
    [InlineData("endswith", ".com", true)]
    public void Evaluate_StringOperators_Work(string op, string value, bool expected)
    {
        var attributeName = op switch
        {
            "contains" or "startswith" or "endswith" => "email",
            _ => "plan"
        };
        var attributeValue = attributeName == "email" ? "john@acme.com" : "premium";
        var rule = TestData.Rule(attributeName, op, value, true);
        var context = new Context("user").AddAttribute(new ContextAttribute(attributeName, new[] { attributeValue }));

        var result = engine.Evaluate(new[] { rule }, context);

        Assert.Equal(expected, result is not null);
    }

    [Theory]
    [InlineData("gt", "18", "25")]
    [InlineData("gte", "18", "18")]
    [InlineData("lt", "18", "15")]
    [InlineData("lte", "18", "18")]
    public void Evaluate_NumericOperators_Work(string op, string value, string actual)
    {
        var rule = TestData.Rule("age", op, value, true);
        var context = new Context("user").AddAttribute(new ContextAttribute("age", new[] { actual }));

        Assert.NotNull(engine.Evaluate(new[] { rule }, context));
    }

    [Fact]
    public void Evaluate_ContextTargets_RequireMatchingType_WhenProvided()
    {
        var rule = new Rule(
            null,
            null,
            null,
            new[] { new RuleCondition("context", "equal", new RuleContextTarget("user-123", "user")) },
            null,
            new FlagValueData(null, new TypedValue(Boolean: true)));

        Assert.NotNull(engine.Evaluate(new[] { rule }, Context.Single("user", "user-123")));
        Assert.Null(engine.Evaluate(new[] { rule }, Context.Single("organization", "user-123")));
    }

    [Fact]
    public void Evaluate_MultipleClauses_UsesAndLogic()
    {
        var rule = new Rule(
            null,
            null,
            null,
            new[]
            {
                new RuleCondition("country", "equal", "US"),
                new RuleCondition("plan", "equal", "premium")
            },
            null,
            new FlagValueData(null, new TypedValue(Boolean: true)));

        var match = new Context("user", identifier: "user-123", attributes: new[]
        {
            new ContextAttribute("country", new[] { "US" }),
            new ContextAttribute("plan", new[] { "premium" })
        });

        var miss = new Context("user", identifier: "user-123", attributes: new[]
        {
            new ContextAttribute("country", new[] { "US" }),
            new ContextAttribute("plan", new[] { "basic" })
        });

        Assert.NotNull(engine.Evaluate(new[] { rule }, match));
        Assert.Null(engine.Evaluate(new[] { rule }, miss));
    }

    [Fact]
    public void IsNull_MatchesWhenAttributeIsAbsent()
    {
        var rule = new Rule(null, null, null, null, null,
            new FlagValueData(null, new TypedValue(Boolean: true)));
        rule = rule with { Criteria = new RuleCondition("tag", "isnull", null) };
        var context = new Context("user", identifier: "u-1");
        Assert.NotNull(engine.Evaluate(new[] { rule }, context));
    }

    [Fact]
    public void IsNull_MatchesWhenAttributeValueIsEmpty()
    {
        var rule = new Rule(null, null, null, null, null,
            new FlagValueData(null, new TypedValue(Boolean: true)));
        rule = rule with { Criteria = new RuleCondition("tag", "isnull", null) };
        var context = new Context("user", identifier: "u-1",
            attributes: new[] { new ContextAttribute("tag", new[] { "" }) });
        Assert.NotNull(engine.Evaluate(new[] { rule }, context));
    }

    [Fact]
    public void NotNull_MatchesWhenAttributeHasValue()
    {
        var rule = new Rule(null, null, null, null, null,
            new FlagValueData(null, new TypedValue(Boolean: true)));
        rule = rule with { Criteria = new RuleCondition("tag", "notnull", null) };
        var context = new Context("user", identifier: "u-1",
            attributes: new[] { new ContextAttribute("tag", new[] { "active" }) });
        Assert.NotNull(engine.Evaluate(new[] { rule }, context));
    }

    [Fact]
    public void NotNull_DoesNotMatchWhenAttributeIsAbsent()
    {
        var rule = new Rule(null, null, null, null, null,
            new FlagValueData(null, new TypedValue(Boolean: true)));
        rule = rule with { Criteria = new RuleCondition("tag", "notnull", null) };
        var context = new Context("user", identifier: "u-1");
        Assert.Null(engine.Evaluate(new[] { rule }, context));
    }

    [Theory]
    [InlineData("notequal")]
    [InlineData("notin")]
    [InlineData("notcontains")]
    [InlineData("notstartswith")]
    [InlineData("notendswith")]
    public void NegativeOperators_MatchWhenAttributeIsAbsent(string op)
    {
        var rule = new Rule(null, null, null, null, null,
            new FlagValueData(null, new TypedValue(Boolean: true)));
        rule = rule with { Criteria = new RuleCondition("missing", op, "x") };
        var context = new Context("user", identifier: "u-1");
        Assert.NotNull(engine.Evaluate(new[] { rule }, context));
    }

    [Theory]
    [InlineData("^foo", "foobar", true)]
    [InlineData("^foo", "barfoo", false)]
    [InlineData("/^foo$/i", "FOO", true)]
    [InlineData("/^foo$/i", "foobar", false)]
    public void Evaluate_RegexOperator_Works(string pattern, string attributeValue, bool expected)
    {
        var rule = TestData.Rule("code", "regex", pattern, true);
        var context = new Context("user").AddAttribute(new ContextAttribute("code", new[] { attributeValue }));

        var result = engine.Evaluate(new[] { rule }, context);

        Assert.Equal(expected, result is not null);
    }

    [Fact]
    public void Evaluate_RegexOperator_FailsClosed_OnInvalidPattern()
    {
        var rule = TestData.Rule("code", "regex", "(unterminated", true);
        var context = new Context("user").AddAttribute(new ContextAttribute("code", new[] { "anything" }));

        Assert.Null(engine.Evaluate(new[] { rule }, context));
    }

    [Fact]
    public void Evaluate_Equal_MatchesAnyOfMultipleConditionValues()
    {
        // Only the second condition value ("CA") matches the attribute — with only the first
        // condition value considered, this would incorrectly fail to match.
        var rule = TestData.Rule("country", "equal", new[] { "US", "CA" }, true);
        var context = new Context("user").AddAttribute(new ContextAttribute("country", new[] { "CA" }));

        Assert.NotNull(engine.Evaluate(new[] { rule }, context));
    }

    [Fact]
    public void Evaluate_NotIn_TrueWhenAtLeastOneAttributeValueIsOutsideList()
    {
        var rule = TestData.Rule("tags", "notin", new[] { "alpha" }, true);
        var context = new Context("user").AddAttribute(new ContextAttribute("tags", new[] { "alpha", "beta" }));

        Assert.NotNull(engine.Evaluate(new[] { rule }, context));
    }

    [Fact]
    public void Evaluate_NotIn_FalseWhenEveryAttributeValueIsInList()
    {
        var rule = TestData.Rule("tags", "notin", new[] { "alpha", "beta" }, true);
        var context = new Context("user").AddAttribute(new ContextAttribute("tags", new[] { "alpha", "beta" }));

        Assert.Null(engine.Evaluate(new[] { rule }, context));
    }

    [Fact]
    public void Evaluate_NotContains_FalseWhenAnyAttributeValueMatchesAnyConditionValue()
    {
        // "beta" contains "bet", so the aggregate positive match exists and notcontains is false —
        // even though no attribute value contains "gamma".
        var rule = TestData.Rule("tags", "notcontains", new[] { "gamma", "bet" }, true);
        var context = new Context("user").AddAttribute(new ContextAttribute("tags", new[] { "alpha", "beta" }));

        Assert.Null(engine.Evaluate(new[] { rule }, context));
    }
}