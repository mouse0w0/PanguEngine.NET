using PanguEngine.Versioning;

namespace PanguEngine.Tests.Versioning;

public sealed class SemVersionTests
{
    [Fact]
    public void ParseReadsStableVersionCore()
    {
        var version = SemVersion.Parse("1.2.3");

        Assert.Equal(1, version.Major);
        Assert.Equal(2, version.Minor);
        Assert.Equal(3, version.Patch);
        Assert.False(version.IsPrerelease);
        Assert.Null(version.Prerelease);
        Assert.Null(version.Metadata);
        Assert.Equal("1.2.3", version.ToString());
    }

    [Fact]
    public void ParseReadsPrereleaseAndMetadata()
    {
        var version = SemVersion.Parse("1.0.0-alpha.1+build.5");

        Assert.True(version.IsPrerelease);
        Assert.Equal("alpha.1", version.Prerelease);
        Assert.Equal("build.5", version.Metadata);
        Assert.Equal("1.0.0-alpha.1+build.5", version.ToString());
    }

    [Fact]
    public void TryParseRejectsNullVersion()
    {
        var parsed = SemVersion.TryParse(null, out var version);

        Assert.False(parsed);
        Assert.Null(version);
    }

    [Fact]
    public void TryParseRejectsCoreNumberOutsideIntRange()
    {
        var parsed = SemVersion.TryParse("2147483648.0.0", out var version);

        Assert.False(parsed);
        Assert.Null(version);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("v1.2.3")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-alpha..1")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3+build..1")]
    [InlineData("1.2.3+build_1")]
    public void TryParseRejectsInvalidVersion(string value)
    {
        var parsed = SemVersion.TryParse(value, out var version);

        Assert.False(parsed);
        Assert.Null(version);
    }

    [Fact]
    public void CompareToOrdersCoreVersionsNumerically()
    {
        Assert.True(SemVersion.Parse("1.9.0") < SemVersion.Parse("1.10.0"));
        Assert.True(SemVersion.Parse("2.0.0") > SemVersion.Parse("1.99.99"));
    }

    [Fact]
    public void CompareToOrdersVersionsBySemVerPrecedence()
    {
        var ordered = new[]
        {
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11",
            "1.0.0-rc.1",
            "1.0.0"
        }.Select(SemVersion.Parse).ToArray();

        for (var i = 0; i < ordered.Length - 1; i++)
            Assert.True(ordered[i] < ordered[i + 1]);
    }

    [Fact]
    public void CompareToIgnoresBuildMetadataButEqualsDoesNot()
    {
        var left = SemVersion.Parse("1.0.0+build.1");
        var right = SemVersion.Parse("1.0.0+build.2");

        Assert.Equal(0, left.CompareTo(right));
        Assert.NotEqual(left, right);
        Assert.True(left != right);
        Assert.False(left == right);
    }

    [Fact]
    public void EqualityOperatorsUseNormalizedVersionText()
    {
        Assert.True(SemVersion.Parse("1.0.0") == SemVersion.Parse("1.0.0"));
        Assert.False(SemVersion.Parse("1.0.0") != SemVersion.Parse("1.0.0"));
    }
}