using PanguEngine.Versioning;

namespace PanguEngine.Tests.Versioning;

public sealed class SemVersionRangeTests
{
    [Fact]
    public void ContainsUsesInclusiveAndExclusiveBounds()
    {
        var range = SemVersionRange.Parse("[1.0.0,2.0.0)");

        Assert.True(range.Contains(SemVersion.Parse("1.0.0")));
        Assert.True(range.Contains(SemVersion.Parse("1.5.0")));
        Assert.False(range.Contains(SemVersion.Parse("2.0.0")));
    }

    [Fact]
    public void ContainsSupportsExactVersion()
    {
        var range = SemVersionRange.Parse("1.2.3");

        Assert.True(range.Contains(SemVersion.Parse("1.2.3")));
        Assert.False(range.Contains(SemVersion.Parse("1.2.4")));
        Assert.False(range.Contains(SemVersion.Parse("1.2.3+build.1")));
    }

    [Fact]
    public void ContainsSupportsOpenUpperBound()
    {
        var range = SemVersionRange.Parse("[1.0.0,)");

        Assert.False(range.Contains(SemVersion.Parse("0.9.9")));
        Assert.True(range.Contains(SemVersion.Parse("1.0.0")));
        Assert.True(range.Contains(SemVersion.Parse("2.0.0")));
    }

    [Fact]
    public void ContainsSupportsOpenLowerBound()
    {
        var range = SemVersionRange.Parse("(,1.0.0]");

        Assert.True(range.Contains(SemVersion.Parse("0.9.9")));
        Assert.True(range.Contains(SemVersion.Parse("1.0.0")));
        Assert.False(range.Contains(SemVersion.Parse("1.0.1")));
    }

    [Fact]
    public void ContainsSupportsMultipleRanges()
    {
        var range = SemVersionRange.Parse("(,1.0.0],[1.2.0,)");

        Assert.True(range.Contains(SemVersion.Parse("0.9.9")));
        Assert.True(range.Contains(SemVersion.Parse("1.0.0")));
        Assert.False(range.Contains(SemVersion.Parse("1.1.0")));
        Assert.True(range.Contains(SemVersion.Parse("1.2.0")));
    }

    [Fact]
    public void ContainsSupportsExactAndRangeMix()
    {
        var range = SemVersionRange.Parse("1.0.0,[1.2.0,)");

        Assert.True(range.Contains(SemVersion.Parse("1.0.0")));
        Assert.False(range.Contains(SemVersion.Parse("1.1.0")));
        Assert.True(range.Contains(SemVersion.Parse("1.2.0")));
    }

    [Fact]
    public void ContainsUsesSemVerPrecedence()
    {
        var range = SemVersionRange.Parse("[1.0.0,1.0.0]");

        Assert.True(range.Contains(SemVersion.Parse("1.0.0+build.1")));
        Assert.False(range.Contains(SemVersion.Parse("1.0.0-alpha")));
    }

    [Fact]
    public void ToStringReturnsNormalizedRange()
    {
        var range = SemVersionRange.Parse("[1.0.0-alpha+001,2.0.0)");

        Assert.Equal("[1.0.0-alpha+001,2.0.0)", range.ToString());
    }

    [Fact]
    public void ToStringReturnsNormalizedMultipleRanges()
    {
        var range = SemVersionRange.Parse("1.0.0,[1.2.0+build.1,)");

        Assert.Equal("1.0.0,[1.2.0+build.1,)", range.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("[1.0.0,]")]
    [InlineData("[,1.0.0]")]
    [InlineData("[,2.0.0)")]
    [InlineData("[2.0.0,1.0.0)")]
    [InlineData("(1.0.0,1.0.0)")]
    [InlineData("(,)")]
    [InlineData("[,]")]
    [InlineData("1.0.0,")]
    [InlineData(",1.0.0")]
    [InlineData("1.0")]
    public void TryParseRejectsInvalidRange(string value)
    {
        var parsed = SemVersionRange.TryParse(value, out var range);

        Assert.False(parsed);
        Assert.Null(range);
    }
}