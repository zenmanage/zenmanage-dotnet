using Zenmanage.Flagging;

namespace Zenmanage.Tests;

public sealed class RolloutEvaluatorTests
{
    [Theory]
    [InlineData("test-salt:user-0", 2211483234u, 34, true, false, false)]
    [InlineData("test-salt:user-2", 1843326798u, 98, false, false, false)]
    [InlineData("abc123:ctx-alpha", 2997997254u, 54, false, false, false)]
    [InlineData("abc123:ctx-beta", 58423103u, 3, true, true, true)]
    [InlineData("rollout-salt:user-100", 2395497973u, 73, false, false, false)]
    [InlineData("rollout-salt:user-200", 2358172588u, 88, false, false, false)]
    [InlineData("fixed-salt:user-42", 1886245039u, 39, true, false, false)]
    public void CrossSdkVectors_MatchSpecification(string input, uint expectedHash, int expectedBucket, bool at50, bool at25, bool at10)
    {
        var parts = input.Split(':');
        var salt = parts[0];
        var identifier = parts[1];

        var hash = RolloutEvaluator.Crc32B(input);

        Assert.Equal(expectedHash, hash);
        Assert.Equal(expectedBucket, (int)(hash % 100));
        Assert.Equal(at50, RolloutEvaluator.IsInBucket(salt, identifier, 50));
        Assert.Equal(at25, RolloutEvaluator.IsInBucket(salt, identifier, 25));
        Assert.Equal(at10, RolloutEvaluator.IsInBucket(salt, identifier, 10));
    }

    [Fact]
    public void IsInBucket_ReturnsFalse_ForNullIdentifier()
    {
        Assert.False(RolloutEvaluator.IsInBucket("salt", null, 100));
    }

    [Fact]
    public void IsInBucket_Throws_ForOutOfRangePercentage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RolloutEvaluator.IsInBucket("salt", "id", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RolloutEvaluator.IsInBucket("salt", "id", 101));
    }
}